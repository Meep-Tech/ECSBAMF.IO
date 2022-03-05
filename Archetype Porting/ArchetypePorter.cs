using Meep.Tech.Collections.Generic;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Meep.Tech.Data.IO {

  /// <summary>
  /// Base statics and accesability stuff for non generic ArchetypePorter access.
  /// </summary>
  public abstract class ArchetypePorter {

    /// <summary>
    /// The base mod folder name
    /// </summary>
    public const string ModFolderName
      = "mods";

    /// <summary>
    /// The imports folder name
    /// </summary>
    public const string ImportFolderName
      = "__imports";

    /// <summary>
    /// The finished imports folder name.
    /// </summary>
    public const string FinishedImportsFolderName
      = "__processed_imports";

    /// <summary>
    /// Option parameter to override the object name
    /// </summary>
    public const string NameOverrideSetting
      = "Name";

    /// <summary>
    /// Option parameter to override the object name
    /// </summary>
    public const string PagkageNameOverrideSetting
      = "PackageName";

    /// <summary>
    /// Option parameter to Move the imported files to the finished imports folder.
    /// Accepts a bool
    /// </summary>
    public const string MoveFinishedFilesToFinishedImportsFolderSetting
      = "MoveImportedFilesToFinished";

    /// <summary>
    /// The name of the config json file.
    /// </summary>
    public const string DefaultConfigFileName = "_config.json";

    /// <summary>
    /// The root mod folder.
    /// </summary>
    public static readonly string RootModsFolder 
      // TOOD: add setting for this root value.
      = Path.Combine("ROOT", ModFolderName);
  }

  /// <summary>
  /// used to im/export archetypes of a specific type from mods
  /// </summary>
  public abstract partial class ArchetypePorter<TArchetype> : ArchetypePorter, IArchetypePorter
    where TArchetype : Meep.Tech.Data.Archetype, IPortableArchetype {

    /// <summary>
    /// The default instance of this type of archetype porter.
    /// </summary>
    public static ArchetypePorter<TArchetype> DefaultInstance {
      get;
      private set;
    }

    /// <summary>
    /// Key for the name value in the config
    /// </summary>
    public const string NameConfigKey = "name";

    /// <summary>
    /// Key for the package name value in the config
    /// </summary>
    public const string PackageNameConfigKey = "packageName";

    /// <summary>
    /// Key for the description in the config
    /// </summary>
    public const string DescriptionConfigKey = "description";

    /// <summary>
    /// Used for a list of tags in json configs
    /// </summary>
    public const string TagsConfigOptionKey
      = "tags";

    /// <summary>
    /// The default package name for archetyps of this type
    /// </summary>
    public abstract string DefaultPackageName {
      get;
    }

    /// <summary>
    /// Keys that work for options for imports.
    /// </summary>
    public virtual HashSet<string> ValidImportOptionKeys
      => new() {
        NameOverrideSetting,
        MoveFinishedFilesToFinishedImportsFolderSetting
      };

    /// <summary>
    /// Valid Keys for the config.json
    /// </summary>
    public virtual HashSet<string> ValidConfigOptionKeys
      => new() {
        NameConfigKey,
        PackageNameConfigKey
      };

    /// <summary>
    /// The user in control of the current game, and imports.
    /// </summary>
    public Func<string> _getCurrentUserName {
      get;
    }

    /// <summary>
    /// The cached archetypes of this kind, by resource id
    /// </summary>
    readonly Dictionary<string, TArchetype> _cachedResources
      = new();

    /// <summary>
    /// The cached archetypes of this kind, by package name then resource id.
    /// </summary>
    readonly Dictionary<string, Dictionary<string, TArchetype>> _cachedResourcesByPackage
      = new();

    /// <summary>
    /// Make a new type of archetype porter with inheritance
    /// </summary>
    protected ArchetypePorter(Func<string> getCurrentUsersUniqueName) {
      _getCurrentUserName = getCurrentUsersUniqueName;
      DefaultInstance ??= this;
    }

    #region Get From Cache

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    /// <param name="resourceKey"></param>
    /// <returns></returns>
    public TArchetype TryToGetGetCachedArchetype(string resourceKey)
      => _cachedResources.TryGetValue(resourceKey, out TArchetype found)
         ? found
         : null;

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    /// <param name="resourceKey"></param>
    /// <returns></returns>
    public TArchetype GetCachedArchetype(string resourceKey)
      => _cachedResources[resourceKey];

    #endregion

    #region Import

    #region Find and Import

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public TArchetype TryToFindAndImportIndividualArchetypeFromModFolder(string resourceKey, Dictionary<string, object> options = null) {
      string modFolder = GetFolderForModItem(resourceKey, out string resourceName, out string packageName);

      // escape safely early
      if (!Directory.Exists(modFolder)) {
        return null;
      }

      string[] parts = resourceKey.Split("::");
      string packageName = parts.First();
      string resourcePath = parts.Last();

      string[] effectedFiles = Directory.GetFiles(modFolder);
      TArchetype archetype
        = _importArchetypesFromExternalFiles(effectedFiles, resourceKey, resourceName, packageName, options)
          .First();

      if (options is not null
        && options.TryGetValue(MoveFinishedFilesToFinishedImportsFolderSetting, out var moveFiles)
        && (bool)moveFiles
      ) {
        _moveFilesToFinishedImportsFolder(archetype.AsSingleItemEnumerable(), effectedFiles, packageName, options);
      }
      _cacheArchetype(archetype, packageName);


      return archetype;
    }

    #endregion

    #endregion

    #region Export


    ///<summary><inheritdoc/></summary>
    public string[] SerializeArchetypeToModFolder(TArchetype archetype)
      => SerializeArchetypeToModFiles(
        archetype,
        GetFolderForArchetype(archetype)
      );

    #endregion

    #region Update and Delete

    #endregion

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public TArchetype LoadArchetypeFromModFolder(string resourceKey, Dictionary<string, object> options = null) {
      string modFolder = GetFolderForModItem(resourceKey, out string name, out string packageName);

      string[] effectedFiles = Directory.GetFiles(modFolder);
      TArchetype archetype
        = _importArchetypesFromExternalFiles(effectedFiles, resourceKey, name, packageName, options)
          .First();

      if (options is not null
        && options.TryGetValue(MoveFinishedFilesToFinishedImportsFolderSetting, out var moveFiles)
          ? (bool)moveFiles
          : false
      ) {
        _moveFilesToFinishedImportsFolder(archetype.AsSingleItemEnumerable(), effectedFiles, packageName, options);
      }
      _cacheArchetype(archetype, packageName);


      return archetype;
    }

    /// <summary>
    /// This searches the Mods folder's Archetype-Sub-Folder for this type, and imports all flat contents using ImportAndBuildNewArchetypesFromLooseFilesAndFolders.
    /// Then this goes though each valid Mod folder file in the provided directory and runs the same on each Archetype-Sub-Folder within them as well.
    /// </summary>
    public IEnumerable<TArchetype> ImportAndBuildArchetypesFromModsFolder(Dictionary<string, object> options) {

    }

    /// <summary>
    /// This searches the __imports directory of the mods folder and looks in the Archetype-Sub-Folder for this type. From therethis imports all flat contents using ImportAndBuildNewArchetypesFromLooseFilesAndFolders.
    /// Then this goes though each valid Mod folder file in the __imports directory and runs the same on each Archetype-Sub-Folder within them as well.
    /// This also packages the results and places the efficient and packaged mods into the mods folder.
    /// </summary>
    public IEnumerable<TArchetype> ImportAndPackageModsFromImportsFolder(Dictionary<string, object> options) {

    }

    /// <summary>
    /// Loose file import first searches for provided json config files (starting with _config.json) and obeys what they say to do.
    /// It then searches for provided folder names, and searches the folder contents for either a json for config, or the first Asset file to import and ignores all other files in these provided directories.
    /// It then goes though the originally provided loose Asset files (such as pngs) and tries to import each as it's own Archetype.
    /// </summary>
    public IEnumerable<TArchetype> ImportAndBuildNewArchetypesFromLooseFilesAndFolders(string[] externalFileAndFolderLocations, Dictionary<string, object> options, out IEnumerable<string> proccessedFiles) {
      List<TArchetype> builtTypes = new();
      List<string> configFiles = new();
      List<string> assetFiles = new();
      List<string> archetypeDirectories = new();

      List<string> allProcessedFiles = new();

      // for each file that doesn't start with `.`, or doesn't start with `_` and isn't named config.json.
      foreach (string providedItem in externalFileAndFolderLocations.Where(f => !f.StartsWith(".") && (!f.StartsWith("_") || f == "_config.json"))) {
        FileAttributes attr = File.GetAttributes(providedItem);

        if (attr.HasFlag(FileAttributes.Directory)) {
          archetypeDirectories.Add(providedItem);
        }
        else {
          assetFiles.Add(providedItem);
          if (Path.GetExtension(providedItem).ToLower() == ".json") {
            configFiles.Add(providedItem);
          }
        }
      }

      // sort alphabetically. This should put _config.json files first too.
      configFiles.Sort(_byNameThenByFolder());
      archetypeDirectories.Sort(_byNameThenByFolder());

      // collect any untouched assets for importing at the end.
      List<string> assetsToTryToBuildLooselyFrom = assetFiles.ToList();

      /// first, try to build all the configs.
      while (configFiles.Any()) {
        string currentConfig = configFiles.First();
        string currentConfigDirectory = Path.GetDirectoryName(currentConfig);
        List<string> assets = assetsToTryToBuildLooselyFrom.ToList();

        // sort assets for this particular config.
        assets.Sort((x, y) => {
          string xDirectory = Path.GetDirectoryName(x);
          string yDirectory = Path.GetDirectoryName(y);
          if (yDirectory == currentConfigDirectory) {
            return 1;
          }
          else if (xDirectory == currentConfigDirectory) {
            return -1;
          }
          else
            return Path.GetFileName(x).CompareTo(Path.GetFileName(y));
        });

        // TODO: add a try here and record failed configs at the end.
        builtTypes.AddRange(
          BuildLooselyFromConfig(JObject.Parse(File.ReadAllText(currentConfig)), assets, options, out var processedFiles)
        );

        // remove proccessed files:
        configFiles.RemoveAt(0);
        if (processedFiles is not null) {
          assetsToTryToBuildLooselyFrom = assetsToTryToBuildLooselyFrom.Except(processedFiles).ToList();
          configFiles = configFiles.Except(processedFiles).ToList();

          allProcessedFiles.AddRange(processedFiles);
        }
      }

      // import directories:
      while (archetypeDirectories.Any()) {
        string currentDirectory = archetypeDirectories.First();
        List<string> folderFiles = Directory.GetFiles(currentDirectory)
          .Where(f => !f.StartsWith(".") && (!f.StartsWith("_") || f == "_config.json"))
          .ToList();
        folderFiles.Sort(_byNameThenByFolder());
        builtTypes.AddRange(
          _buildAllFromSingleFolder(
            currentDirectory,
            folderFiles,
            options,
            out var processedFiles
          )
        );

        // remove proccessed files:
        archetypeDirectories.RemoveAt(0);
        if (processedFiles is not null) {
          assetsToTryToBuildLooselyFrom = assetsToTryToBuildLooselyFrom.Except(processedFiles).ToList();
          configFiles = configFiles.Except(processedFiles).ToList();

          allProcessedFiles.AddRange(processedFiles);
        }
      }

      assetsToTryToBuildLooselyFrom.Sort(_byNameThenByFolder());
      // import loose files:
      while (assetsToTryToBuildLooselyFrom.Any()) {
        string currentAsset = assetsToTryToBuildLooselyFrom.First();
        builtTypes.AddRange(
          BuildLooselyFromAssets(
            assetsToTryToBuildLooselyFrom,
            options,
            out var processedFiles
          )
        );

        // remove proccessed files:
        assetsToTryToBuildLooselyFrom.RemoveAt(0);
        if (processedFiles is not null) {
          assetsToTryToBuildLooselyFrom = assetsToTryToBuildLooselyFrom.Except(processedFiles).ToList();

          allProcessedFiles.AddRange(processedFiles);
        }
      }

      proccessedFiles = allProcessedFiles;
      return builtTypes;
    }

    protected abstract IEnumerable<TArchetype> BuildLooselyFromConfig(JObject config, IEnumerable<string> assetFiles, Dictionary<string, object> options, out IEnumerable<string> processedFiles);

    protected abstract IEnumerable<TArchetype> BuildLooselyFromAssets(IEnumerable<string> assetFiles, Dictionary<string, object> options, out IEnumerable<string> processedFiles);

    /// <summary>
    /// Serialize this archetype to a set of files in the mod folder.
    /// </summary>
    /// <param name="archetype">The archetype to serialize into a file or files</param>
    /// <param name="packageDirectoryPath">The root path to save files to for this archetype</param>
    /// <returns>The newly serialized file's locations</returns>
    protected abstract string[] SerializeArchetypeToModFiles(TArchetype archetype, string packageDirectoryPath);

    /// <summary>
    /// This processes this folder, and all sub folders, as "single archetype folders".
    /// This means it will search this (and each sub folder if recusive is enabled) for a single config file, or asset to build an archetype form, ignoring files wthat begin with . or _
    /// This just ignores directories with no valid items as well. 
    /// </summary>
    IEnumerable<TArchetype> _buildAllFromSingleFolder(string folderLocation, IEnumerable<string> folderFiles, Dictionary<string, object> options, out IEnumerable<string> processedFiles, bool recursive = true) {
      List<string> folderItems = folderFiles.ToList();
      List<TArchetype> builtTypes = new();
      List<string> allProccessedFiles = new();

      // remove folders
      foreach (string providedItem in folderFiles) {
        if (recursive) {
          // if we're doing recursive, check each folder too
          FileAttributes attr = File.GetAttributes(providedItem);
          if (attr.HasFlag(FileAttributes.Directory)) {
            builtTypes.AddRange(_buildAllFromSingleFolder(folderLocation, folderFiles, options, out var processed, recursive));
            if (processed is not null) {
              allProccessedFiles.AddRange(processed);
            }
          }
        }

        folderItems.Remove(providedItem);
      }

      // check if there's a config
      string configFile;
      if ((configFile = folderFiles.FirstOrDefault(f => Path.GetExtension(f).ToLower() == ".json")) is not null) {
        // TODO: add a try here and record failed configs at the end.
        builtTypes.AddRange(BuildLooselyFromConfig(JObject.Parse(File.ReadAllText(configFile)), folderFiles.Except(configFile.AsSingleItemEnumerable()), options, out var processed));
        allProccessedFiles.Add(configFile);
        if (processed is not null) {
          allProccessedFiles.AddRange(processed);
        }
      } // if not just build from the files, with preference alphabetically 
      else {
        builtTypes.AddRange(BuildLooselyFromAssets(folderFiles, options, out var processed));
        if (processed is not null) {
          allProccessedFiles.AddRange(processed);
        }
      }

      processedFiles = allProccessedFiles;
      return builtTypes;
    }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public string GetFolderForArchetype(IPortableArchetype portableArchetype) {
      TArchetype archetype = (TArchetype)portableArchetype;
      return GetFolderForModItem(archetype.Id.Name, archetype.PackageKey);
    }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public string GetFolderForModItem(string resourceKey, out string resourceName, out string packageName) {
      packageName = null;
      string[] keyParts = resourceKey.Split("::");
      if(keyParts.Length == 1) {
        resourceName = resourceKey;
      } else if(keyParts.Length == 2) {
        resourceName = keyParts[1];
        packageName = keyParts[0];
      } else
        throw new ArgumentException($"'::' cannot be used in backage names or resource names");
      return GetFolderForModItem(resourceName, packageName);
    }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public string GetFolderForModItem(string name, string packageName = null) {
      var modFolder = RootModsFolder;
      if(packageName is null) {
        modFolder = Path.Combine(modFolder, DefaultPackageName, name.Replace(".", "/"));
      } else {
        modFolder = Path.Combine(modFolder, packageName.Replace(".", "/"), DefaultPackageName, name.Replace(".", "/"));
      }

      return modFolder;
    }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public bool TryToMoveRenamedArchetypeFolder(string oldName, IPortableArchetype archetype) {
      string newFolderName = GetFolderForArchetype(archetype);

      if(System.IO.Directory.Exists(newFolderName)) {
        return false;
      }

      string oldFolderName = GetFolderForModItem(oldName, archetype.PackageKey);
      Directory.CreateDirectory(newFolderName);
      _copyDirectory(oldFolderName, newFolderName, true);
      return true;
    }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public void ForceMoveRenamedArchetypeFolder(string oldName, IPortableArchetype archetype) {
      string newFolderName = GetFolderForArchetype(archetype);

      /// empty the target folder if it already exists.
      if(System.IO.Directory.Exists(newFolderName)) {
        DirectoryInfo directoryInfo = new(newFolderName);
        directoryInfo.EnumerateFiles().ForEach(file => file.Delete());
        directoryInfo.EnumerateDirectories().ForEach(subDirectory => subDirectory.Delete(true));
      }

      string oldFolderName = GetFolderForModItem(oldName, archetype.PackageKey);
      Directory.CreateDirectory(newFolderName);
      _copyDirectory(oldFolderName, newFolderName, true);
    }

    /// <summary>
    /// Construct the keys for a type given the main asset file, config, and options.
    /// </summary>
    protected virtual (string resourceName, string packageName, string resourceKey) ConstructArchetypeKeys(
      string primaryAssetFilename,
      bool fromSingleArchetypeFolder,
      Dictionary<string, object> options,
      JObject config
    ) {
      string resourceName;
      string packageName;
      string resourceKey;

      /// Resource Name
      // check the options for an override first
      if (options.TryGetValue(NameConfigKey, out var name)) {
        resourceName = name as string;
      } // check json config next
      else if (config.TryGetValue(NameConfigKey, out JToken nameToken)) {
        resourceName = nameToken.Value<string>();
      } else {
        // if it's a loost asset, we use the asset name
        if (!fromSingleArchetypeFolder) {
          resourceName = Path.GetFileName(primaryAssetFilename);
        }

        // if it's in a folder, we need to find the right name to use so we can find it again~
        // this longer name will be trimmed before being returned, but is used to make the resourceKey
        var currentFolder = new DirectoryInfo(primaryAssetFilename);

        resourceName = "";
        while (currentFolder.Parent != null && currentFolder.Parent.Name != DefaultPackageName) {
          resourceName += currentFolder.Name + "/";
        }

        // we went too far, set it to just the filename, unless the file name is _config:
        if (resourceName == "" || currentFolder.Parent == null) {
          resourceName = Path.GetFileName(primaryAssetFilename);
        }
        else {
          resourceName = resourceName.Trim('/').Trim();
        }

        if (resourceName == "_config") {
          throw new ArgumentException($"_cofig cannot be the name of a resource. Please provide a resource name under the 'name' property in the config");
        }
      }

      if (resourceName is null) {
        throw new ArgumentNullException(NameConfigKey);
      }

      /// Package Name
      if (options.TryGetValue(NameConfigKey, out var package)) {
        packageName = package as string;
      } // check json config next
      else if (config.TryGetValue(NameConfigKey, out JToken packageToken)) {
        packageName = packageToken.Value<string>();
      }
      else {
        var currentFolder = new DirectoryInfo(primaryAssetFilename);

        if (currentFolder.Parent.Name != ImportFolderName
           && currentFolder.Parent.Name != ModFolderName
         ) {
          packageName = null;
          currentFolder = currentFolder.Parent;
          while (currentFolder.Parent.Name != ImportFolderName
            && currentFolder.Parent.Name != ModFolderName
            && currentFolder.Parent != null
          ) {
            packageName = currentFolder.Name;
            currentFolder = currentFolder.Parent;
          }

          // we went too far... use the default.
          if (currentFolder.Parent == null || package == null) {
            packageName = GetDefaultPackageName();
          }
        }
        else packageName = GetDefaultPackageName();
      }

      if (resourceName is null) {
        throw new ArgumentNullException(NameConfigKey);
      }

      resourceKey = packageName + "::" + resourceName;

      if (resourceName.Contains('/')) {
        resourceName = Path.GetFileName(resourceName);
      }

      return (resourceName, packageName, resourceKey);
    }

    /// <summary>
    /// Get the default package name
    /// </summary>
    protected string GetDefaultPackageName()
      => _getCurrentUserName() + "'s Custom Assets";

    /// <summary>
    /// Try to get the _config.json from the set of provided files.
    /// </summary>
    protected JObject TryToGetConfig(IEnumerable<string> externalFileLocations, out string configFileName) {
      configFileName = externalFileLocations
        .FirstOrDefault(fileName => fileName == DefaultConfigFileName);
      if (configFileName is null) {
        configFileName = externalFileLocations
          .FirstOrDefault(fileName => Path.GetExtension(fileName).ToLower() == ".json");
      }
      if (configFileName is not null && File.Exists(configFileName)) {
        return JObject.Parse(
          File.ReadAllText(configFileName)
        );
      }
      else
        return new JObject();
    }

    static void _copyDirectory(string sourceDir, string destinationDir, bool recursive) {
      // Get information about the source directory
      var dir = new DirectoryInfo(sourceDir);

      // Check if the source directory exists
      if(!dir.Exists)
        throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

      // Cache directories before we start copying
      DirectoryInfo[] dirs = dir.GetDirectories();

      // Create the destination directory
      Directory.CreateDirectory(destinationDir);

      // Get the files in the source directory and copy to the destination directory
      foreach(FileInfo file in dir.GetFiles()) {
        string targetFilePath = Path.Combine(destinationDir, file.Name);
        file.CopyTo(targetFilePath);
      }

      // If recursive and copying subdirectories, recursively call this method
      if(recursive) {
        foreach(DirectoryInfo subDir in dirs) {
          string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
          _copyDirectory(subDir.FullName, newDestinationDir, true);
        }
      }
    }

    void _cacheArchetype(TArchetype archetype, string packageName = null) {
      _cachedResources.Add(archetype.ResourceKey, archetype);
      if(_cachedResourcesByPackage.TryGetValue(packageName ?? "", out var existingSet)) {
        existingSet.Add(archetype.ResourceKey, archetype);
      } else if(!string.IsNullOrWhiteSpace(packageName)) {
        _cachedResourcesByPackage.Add(packageName, new() {
          {
            archetype.ResourceKey,
            archetype
          }
        });
      }
    }

    void _moveFilesToFinishedImportsFolder(IEnumerable<TArchetype> compiledArchetypes, string[] fileNames, string packageName = null, Dictionary<string, object> options = null) {
      /// Save files that are re-compiled for speed to the mod folder:
      foreach(TArchetype compiled in compiledArchetypes) {
        SerializeArchetypeToModFiles(compiled, GetFolderForArchetype(compiled));
      }

      /// Move the old files to exports
      string exportFolder
        = Path.Combine(RootModsFolder, IArchetypePorter.FinishedImportsFolderName, packageName ?? compiledArchetypes.First().DefaultPackageKey);
      if(packageName is not null) {
        exportFolder = Path.Combine(exportFolder, compiledArchetypes.First().DefaultPackageKey);
      }

      // Move each untouched file to output:
      Directory.CreateDirectory(exportFolder);
      foreach(string fileName in fileNames) {
        System.IO.File.Move(fileName, Path.Combine(exportFolder, Path.GetFileName(fileName)));
        // TODO: these any file lookups could probably be quicker:
        if(!Directory.GetParent(fileName).GetFiles().Any()) {
          if(Directory.GetParent(fileName).Name == IArchetypePorter.ImportFolderName) {
            throw new Exception($"Folder deleting wrong");
          }
          Directory.GetParent(fileName).Delete();
        }

        if(packageName is not null) {
          if(!Directory.GetParent(fileName).GetFiles().Any() && !Directory.GetParent(fileName).GetDirectories().Any()) {
            if(Directory.GetParent(fileName).Name == IArchetypePorter.ImportFolderName) {
              throw new Exception($"Folder deleting gone wrong");
            }
            Directory.GetParent(fileName).Parent.Delete();
          }
        }
      }
    }
    static Comparison<string> _byNameThenByFolder() {
      return (string x, string y) => {
        string fileA = Path.GetFileName(x);
        string fileB = Path.GetFileName(y);
        if (fileA != fileB) {
          return fileA.CompareTo(fileB);
        }

        else return x.CompareTo(y);
      };
    }

    #region IPorter
    IEnumerable<Archetype> IArchetypePorter.ImportAndBuildNewArchetypesFromLooseFilesAndFolders(string[] externalFileAndFolderLocations, Dictionary<string, object> options, out IEnumerable<string> processedFiles)
      => ImportAndBuildNewArchetypesFromLooseFilesAndFolders(externalFileAndFolderLocations, options, out processedFiles);

    IEnumerable<Archetype> IArchetypePorter.ImportAndBuildNewArchetypesFromModsFolder(Dictionary<string, object> options)
      => ImportAndBuildArchetypesFromModsFolder(options);

    IEnumerable<Archetype> IArchetypePorter.ImportAndPackageModsFromImportsFolder(Dictionary<string, object> options)
      => ImportAndPackageModsFromImportsFolder(options);

    Archetype IArchetypePorter.GetCachedArchetype(string resourceKey)
      => GetCachedArchetype(resourceKey);

    Archetype IArchetypePorter.TryToGetGetCachedArchetype(string resourceKey)
      => TryToGetGetCachedArchetype(resourceKey);

    Archetype IArchetypePorter.LoadArchetypeFromModFolder(string resourceKey, Dictionary<string, object> options)
      => LoadArchetypeFromModFolder(resourceKey, options);

    Archetype IArchetypePorter.TryToFindArchetypeAndLoadFromModFolder(string resourceKey, Dictionary<string, object> options)
      => TryToFindAndImportIndividualArchetypeFromModFolder(resourceKey, options);

    string[] IArchetypePorter.SerializeArchetypeToModFolder(Archetype archetype)
      => SerializeArchetypeToModFolder((TArchetype)archetype);

    #endregion

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    /*public IEnumerable<TArchetype> ImportAndBuildNewArchetypeFromFile(string externalFileLocation, Dictionary<string, object> options = null) {
      string name = options is not null && options.TryGetValue(IArchetypePorter.NameOverrideSetting, out var nameObj)
         ? (string)nameObj
         : null;
      string packageName = options is not null && options.TryGetValue(IArchetypePorter.PagkageNameOverrideSetting, out var pkgNameObj)
         ? (string)pkgNameObj
         : null;

      string resourceKey = GetResourceKeyFromFileLocationAndSettings(externalFileLocation, ref packageName, ref name);
      if (_cachedResources.ContainsKey(resourceKey)) {
        int incrementor = 0;
        string fixedKey;
        do {
          fixedKey = resourceKey + $" ({++incrementor})";
        } while (_cachedResources.ContainsKey(fixedKey));

        resourceKey = fixedKey;
      }

      List<TArchetype> archetypes
        = _importArchetypesFromExternalFile(externalFileLocation, resourceKey, name, packageName, options)
          .ToList();

      if (options is not null
        && options.TryGetValue(IArchetypePorter.MoveFinishedFilesToFinishedImportsFolderSetting, out var moveFiles)
          ? (bool)moveFiles
          : false
      ) {
        _moveFilesToFinishedImportsFolder(archetypes, new string[] { externalFileLocation }, packageName, options);
      }

      foreach (TArchetype archetype in archetypes) {
        _cacheArchetype(archetype, packageName);
      }

      return archetypes;
    }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public IEnumerable<TArchetype> ImportAndBuildNewArchetypeFromFolder(string externalFolderLocation, Dictionary<string, object> options) {
      string name = options is not null && options.TryGetValue(IArchetypePorter.NameOverrideSetting, out var nameObj)
         ? (string)nameObj
         : null;

      if (name is null) {
        name = Path.GetDirectoryName(externalFolderLocation);
      }

      string packageName = null;
      string resourceKey = GetResourceKeyFromFileLocationAndSettings(externalFolderLocation, ref packageName, ref name);
      if (_cachedResources.ContainsKey(resourceKey)) {
        int incrementor = 0;
        string fixedKey;
        do {
          fixedKey = resourceKey + $" ({++incrementor})";
        } while (_cachedResources.ContainsKey(fixedKey));

        resourceKey = fixedKey;
      }

      string[] effectedFiles = Directory.GetFiles(externalFolderLocation);
      IEnumerable<TArchetype> archetypes = _importArchetypesFromExternalFiles(effectedFiles, resourceKey, name, packageName, options);

      if (options is not null
        && options.TryGetValue(IArchetypePorter.MoveFinishedFilesToFinishedImportsFolderSetting, out var moveFiles)
          ? (bool)moveFiles
          : false
      ) {
        _moveFilesToFinishedImportsFolder(archetypes, effectedFiles, packageName, options);
      }
      foreach (TArchetype archetype in archetypes) {
        _cacheArchetype(archetype, packageName);
      }


      return archetypes;
    }
    
    public IEnumerable<TArchetype> ImportAndBuildNewArchetypeFromFiles(string[] externalFileLocations, Dictionary<string, object> options) {
      string name = options is not null && options.TryGetValue(IArchetypePorter.NameOverrideSetting, out var nameObj)
         ? (string)nameObj
         : null;

      string defaultNameFile = externalFileLocations.First(fileName => fileName != IArchetypePorter.ConfigFileName);
      string packageName = null;
      string resourceKey = GetResourceKeyFromFileLocationAndSettings(defaultNameFile, ref packageName, ref name);
      if(_cachedResources.ContainsKey(resourceKey)) {
        int incrementor = 0;
        string fixedKey;
        do {
          fixedKey = resourceKey + $" ({++incrementor})";
        } while(_cachedResources.ContainsKey(fixedKey));

        resourceKey = fixedKey;
      }

      IEnumerable<TArchetype> archetypes
        = _importArchetypesFromExternalFiles(externalFileLocations, resourceKey, name, packageName, options);

      if(options is not null
        && options.TryGetValue(IArchetypePorter.MoveFinishedFilesToFinishedImportsFolderSetting, out var moveFiles)
        && (bool)moveFiles
      ) {
        _moveFilesToFinishedImportsFolder(archetypes, externalFileLocations, packageName, options);
      }

      foreach(TArchetype archetype in archetypes) {
        _cacheArchetype(archetype);
      }

      return archetypes;
    }*/

    /// <summary>
    /// Used to make a new key for a new resouce made by the current user
    /// </summary>
    /*public virtual string GetResourceKeyFromFileLocationAndSettings(string externalFileLocation, ref string packageName, ref string name) {
      string key = "";
      string packageFolderKey = "";
      string nameFolderKey = "";
      if(packageName is null || name is null) {
        var currentFolder = new DirectoryInfo(externalFileLocation);

        if(name is null) {
          while(currentFolder.Name != DefaultPackageName) {
            nameFolderKey = currentFolder.Name + "." + nameFolderKey;
            currentFolder = currentFolder.Parent;
          }
        }

        if(packageName is null
          && currentFolder.Parent.Name != IArchetypePorter.ImportFolderName
          && currentFolder.Parent.Name != IArchetypePorter.ModFolderName
        ) {
          currentFolder = currentFolder.Parent;
          while(currentFolder.Parent.Name != IArchetypePorter.ImportFolderName
            && currentFolder.Parent.Name != IArchetypePorter.ModFolderName) {
            packageFolderKey = currentFolder.Name + "." + packageFolderKey;
            currentFolder = currentFolder.Parent;
          }
        }

        packageName ??= packageFolderKey.Trim('.');
        name ??= nameFolderKey.Trim('.');
      }

      if(!string.IsNullOrWhiteSpace(packageName)) {
        key += packageName + "::";
      }

      return key + (name = Path.GetFileNameWithoutExtension(name ?? externalFileLocation));
    }

    /// <summary>
    /// Correct package name, resource key, etc according to the config values:
    /// </summary>
    protected string CorrectBaseKeysAndNamesForConfigValues(string externalFileLocation, ref string name, ref string packageKey, JObject config) {
      name = config.TryGetValue(NameConfigKey, out JToken value)
        ? value.Value<string>()
        : name;
      packageKey = config.TryGetValue(PackageNameConfigKey, out JToken value2)
        ? value2.Value<string>()
        : packageKey;
      return GetResourceKeyFromFileLocationAndSettings(
         externalFileLocation,
         ref packageKey,
         ref name
       );
    }*/
  }
}

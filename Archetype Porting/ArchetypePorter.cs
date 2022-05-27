using Meep.Tech.Collections.Generic;
using Meep.Tech.Data.IO;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;

namespace Meep.Tech.Data.IO {

  /// <summary>
  /// Base statics and accesability stuff for non generic ArchetypePorter access.
  /// </summary>
  public abstract class ArchetypePorter {

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
    /// The imports folder name
    /// </summary>
    public const string ImportFolderName
      = "__imports";

    /// <summary>
    /// The finished imports folder name.
    /// </summary>
    public const string ProcessedImportsFolderName
      = "__processed_imports";

    /// <summary>
    /// The finished imports folder name.
    /// </summary>
    public const string UnProccessedImportsFolderName
      = "__unprocessed_imports";

    /// <summary>
    /// The finished imports folder name.
    /// </summary>
    public const string PluginsSubFolderName
      = "_plugins";

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
    /// Option parameter specifying a set of files to import came from an Single Archetype Sub-Folder.
    /// Accepts a bool
    /// </summary>
    public const string FromSingleArchetypeFolderImportOptionsKey
      = "FromSingleArchetypeFolder";

    /// <summary>
    /// The name of the config json file.
    /// </summary>
    public const string DefaultConfigFileName 
      = "_config.json";

    /// <summary>
    /// The universe this imports into
    /// </summary>
    public Universe Universe
      => _universe ??= Archetypes.DefaultUniverse;
    internal Universe _universe;

    /// <summary>
    /// Quick access to the root mods folder being used.
    /// </summary>
    protected string RootModsFolder
      => Universe.GetModData().RootModsFolder;

    /// <summary>
    /// Keys that work for options for imports.
    /// </summary>
    public virtual HashSet<string> ValidImportOptionKeys
      => new() {
        NameOverrideSetting,
        FromSingleArchetypeFolderImportOptionsKey
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
    /// The default package name for archetyps of this type
    /// </summary>
    public abstract string SubFolderName {
      get;
    }

    /// <summary>
    /// The base type of archetype this imports.
    /// </summary>
    public abstract Type ArchetypeBaseType { 
      get;
    }

    /// <summary>
    /// Helper to filter out invalid files for porters.
    /// </summary>
    public static IEnumerable<string> FilterOutInvalidFilenames(IEnumerable<string> externalFileAndFolderLocations, bool allowSpecialConfig = true)
      => externalFileAndFolderLocations.Where(f => !f.StartsWith(".") && (!f.StartsWith("_") || (allowSpecialConfig && f == "_config.json")));

    /// <summary>
    /// Helper function to get all the valid flat files and directory names that the importer uses.
    /// </summary>
    public static IEnumerable<string> GetValidFlatFilesAndDirectoriesFromDirectory(string folder) 
      => FilterOutInvalidFilenames(Directory.GetFiles(folder).Concat(Directory.GetDirectories(folder).ToHashSet()));
  }

  /// <summary>
  /// used to im/export archetypes of a specific type from mods
  /// </summary>
  public abstract partial class ArchetypePorter<TArchetype> : ArchetypePorter, IArchetypePorter
    where TArchetype : Meep.Tech.Data.Archetype, IPortableArchetype {

    ///<summary><inheritdoc/></summary>
    public override Type ArchetypeBaseType 
      => typeof(TArchetype);

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
    protected ArchetypePorter(Func<string> getCurrentUsersUniqueName, Universe universe = null) {
      _getCurrentUserName = getCurrentUsersUniqueName;
      _universe = universe;
    }

    #region Get

    #region Archetype From Cache

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

    #region Folders and Keys

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public string GetFolderFor(TArchetype portableArchetype) {
      TArchetype archetype = portableArchetype;
      return GetArchetypeFolderFromKey(archetype.ResourceKey);
    }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public string GetArchetypeFolderAndDeconstructKey(string resourceKey, out string resourceName, out string packageName) {
      string resourceLocation;
      string[] keyParts = resourceKey.Split("::");
      if (keyParts.Length == 2) {
        resourceLocation = keyParts[1];
        packageName = keyParts[0];
      }
      else
        throw new ArgumentException($"'::' cannot be used in package names or resource names. All resource keys require a package and location key");

      resourceName = Path.GetFileNameWithoutExtension(resourceLocation);
      return Path.Combine(RootModsFolder, packageName, SubFolderName, new DirectoryInfo(resourceLocation).FullName);
    }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public string GetArchetypeFolderFromKey(string resourceKey) {
      string resourceLocation;
      string packageName;
      string[] keyParts = resourceKey.Split("::");
      if (keyParts.Length == 2) {
        resourceLocation = keyParts[1];
        packageName = keyParts[0];
      }
      else
        throw new ArgumentException($"'::' cannot be used in package names or resource names. All resource keys require a package and location key");

      return Path.Combine(RootModsFolder, packageName, SubFolderName, new DirectoryInfo(resourceLocation).FullName);
    }

    /// <summary>
    /// Construct the keys for a type given the main asset file, config, and options.
    /// </summary>
    public virtual (string resourceName, string packageName, string resourceKey) ConstructArchetypeKeys(
      string primaryAssetFilename,
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
      else if (config == null || (resourceName = config.TryGetValue<string>(NameConfigKey)) == null) {
        // if it's a loost asset, we use the asset name
        if (!(options.TryGetValue(FromSingleArchetypeFolderImportOptionsKey, out var found) && found is bool foundBool && foundBool)) {
          resourceName = Path.GetFileNameWithoutExtension(primaryAssetFilename);
        } // if it's in a folder, we need to find the right name to use so we can find it again~
          // this longer name will be trimmed before being returned, but is used to make the resourceKey
        else {
          var currentFolder = new DirectoryInfo(Path.GetDirectoryName(primaryAssetFilename));

          resourceName = "";
          while (currentFolder.Parent != null && currentFolder.Parent.Name != SubFolderName) {
            resourceName = currentFolder.Name + "/" + resourceName;
            currentFolder = currentFolder.Parent;
          }

          // we went too far, set it to just the filename, unless the file name is _config:
          if (resourceName == "" || currentFolder.Parent == null) {
            resourceName = Path.GetDirectoryName(primaryAssetFilename);
          }
          else {
            resourceName = resourceName.Trim('/').Trim();
          }

          if (resourceName == "_config") {
            throw new ArgumentException($"_cofig cannot be the name of a resource. Please provide a resource name under the 'name' property in the config");
          }
        }
      }

      if (resourceName is null) {
        throw new ArgumentNullException(NameConfigKey);
      }

      /// Package Name
      if (options.TryGetValue(PackageNameConfigKey, out var package)) {
        packageName = package as string;
      } // check json config next
      else if (config == null || (packageName = config.TryGetValue<string>(PackageNameConfigKey)) == null) {
        var currentFolder = new DirectoryInfo(primaryAssetFilename);

        if (currentFolder.Parent.Name != ImportFolderName
           && currentFolder.Parent.Name != ModPorterContext.ModFolderName
         ) {
          packageName = null;
          currentFolder = currentFolder.Parent;
          while (currentFolder.Parent != null
            && currentFolder.Parent.Name != ImportFolderName
            && currentFolder.Parent.Name != ModPorterContext.ModFolderName
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
        throw new ArgumentNullException(PackageNameConfigKey);
      }

      resourceKey = packageName + "::" + resourceName;

      if (resourceName.Contains('/')) {
        resourceName = Path.GetFileNameWithoutExtension(resourceName);
      }

      return (resourceName, packageName, resourceKey);
    }

    /// <summary>
    /// Get the default package name
    /// </summary>
    public string GetDefaultPackageName()
      => _getCurrentUserName() + "'s Custom Assets";

    #endregion

    #region Helpers

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

    #endregion

    #endregion

    #region Import

    #region Find and Import

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public TArchetype TryToFindAndImportIndividualArchetypeFromModFolder(string resourceKey, Dictionary<string, object> options = null) {
      string modFolder = GetArchetypeFolderAndDeconstructKey(resourceKey, out _, out string packageName);

      // escape safely early
      if (!Directory.Exists(modFolder)) {
        return null;
      }

      string[] effectedFiles = FilterOutInvalidFilenames(Directory.GetFiles(modFolder)).ToArray();
      TArchetype archetype
        = BuildAllArchetypesFromSingleArchetypeFolder(modFolder, effectedFiles, options, out _, false)
          .First();

      _cacheArchetype(archetype, packageName);

      return archetype;
    }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public TArchetype ImportIndividualArchetypeFromModFolder(string resourceKey, Dictionary<string, object> options = null) {
      string modFolder = GetArchetypeFolderAndDeconstructKey(resourceKey, out _, out string packageName);

      string[] effectedFiles = Directory.GetFiles(modFolder);
      TArchetype archetype
        = BuildAllArchetypesFromSingleArchetypeFolder(modFolder, effectedFiles, options, out _, false)
          .First();

      _cacheArchetype(archetype, packageName);

      return archetype;
    }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public IEnumerable<TArchetype> TryToFindAndImportMod(string modPackageNameOrResourceKey, Dictionary<string, object> options = null) {
      string modPackageName = modPackageNameOrResourceKey;
      if (modPackageNameOrResourceKey.Contains("::")) {
        modPackageName = modPackageNameOrResourceKey.Split("::").First();
      }
      string modPackageFolder = Path.Combine(RootModsFolder, modPackageName);

      // escape safely early
      if (!Directory.Exists(modPackageFolder)) {
        return null;
      }

      return _importAllOfThisTypeFromModFolder(modPackageFolder, options);
    }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public bool TryToFindAndImportMod(string modPackageNameOrResourceKey, out IEnumerable<TArchetype> importedTypes, Dictionary<string, object> options = null) {
      string modPackageName = modPackageNameOrResourceKey;
      if (modPackageNameOrResourceKey.Contains("::")) {
        modPackageName = modPackageNameOrResourceKey.Split("::").First();
      }
      string modPackageFolder = Path.Combine(RootModsFolder, modPackageName);

      // escape safely early
      if (!Directory.Exists(modPackageFolder)) {
        return (importedTypes = Enumerable.Empty<TArchetype>()).Any();
      }

      importedTypes = _importAllOfThisTypeFromModFolder(modPackageFolder, options);
      return importedTypes.Any();
    }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public IEnumerable<TArchetype> ImportMod(string modPackageNameOrResourceKey, Dictionary<string, object> options = null) {
      string modPackageName = modPackageNameOrResourceKey;
      if (modPackageNameOrResourceKey.Contains("::")) {
        modPackageName = modPackageNameOrResourceKey.Split("::").First();
      }
      string modPackageFolder = Path.Combine(RootModsFolder, modPackageName);

      return _importAllOfThisTypeFromModFolder(modPackageFolder, options);
    }

    #endregion

    #region Bulk Import

    /// <summary>
    /// This searches the Mods folder's Archetype-Sub-Folder for this type, and imports all flat contents using ImportAndBuildNewArchetypesFromLooseFilesAndFolders.
    /// Then this goes though each valid Mod folder file in the provided directory and runs the same on each Archetype-Sub-Folder within them as well.
    /// </summary>
    public IEnumerable<TArchetype> ImportAndBuildArchetypesFromModsFolder(Dictionary<string, object> options) {
      List<TArchetype> builtTypes = new();

      // get all mod packages:
      foreach (string modFolder in FilterOutInvalidFilenames(Directory.GetDirectories(RootModsFolder))) {
        builtTypes.AddRange(_importAllOfThisTypeFromModFolder(modFolder, options));
      }

      return builtTypes;
    }

    IEnumerable<TArchetype> _importAllOfThisTypeFromModFolder(string modFolder, Dictionary<string, object> options) {
      //List<TArchetype> builtTypes = new();
      // get sub folders for each of them:
      /*foreach ((string folder, ArchetypePorter porter) in Directory.GetDirectories(modFolder)
        .Where(f => Universe.GetModData().PortersByArchetypeSubfolder.ContainsKey(Path.GetDirectoryName(f)))
        .Select(f => (folder: f, porter: Universe.GetModData().PortersByArchetypeSubfolder[Path.GetDirectoryName(f)]))
      ) {*/
        /*IEnumerable<TArchetype> typesFromFolder =*/return ImportAndBuildNewArchetypesFromLooseFilesAndFolders(
         GetValidFlatFilesAndDirectoriesFromDirectory(modFolder),
         options,
         out _
        );

        //builtTypes.AddRange(typesFromFolder);
      //}

      //return builtTypes;
    }

    /// <summary>
    /// This searches the __imports directory of the mods folder and looks in the Archetype-Sub-Folder for this type. From therethis imports all flat contents using ImportAndBuildNewArchetypesFromLooseFilesAndFolders.
    /// Then this goes though each valid Mod folder file in the __imports directory and runs the same on each Archetype-Sub-Folder within them as well.
    /// This also packages the results and places the efficient and packaged mods into the mods folder.
    /// </summary>
    public IEnumerable<TArchetype> ImportAndPackageModsFromImportsFolder(Dictionary<string, object> options) {
      List<TArchetype> builtTypes = new();
      List<string> proccessedFilesToMove = new();

      /// import all archetypes from the __imports folder
      string importsFolder = Path.Combine(RootModsFolder, ImportFolderName);
      // get all mod packages first:
      foreach (string modFolder in FilterOutInvalidFilenames(Directory.GetDirectories(importsFolder))) {
        // get sub folders for each of them:
        foreach ((string folder, ArchetypePorter porter) in Directory.GetDirectories(modFolder)
          .Where(f => Universe.GetModData().PortersByArchetypeSubfolder.ContainsKey(Path.GetDirectoryName(f)))
          .Select(f => (folder: f, porter: Universe.GetModData().PortersByArchetypeSubfolder[Path.GetDirectoryName(f)]))
        ) {
          IEnumerable<TArchetype> typesFromFolder = ImportAndBuildNewArchetypesFromLooseFilesAndFolders(
           GetValidFlatFilesAndDirectoriesFromDirectory(folder),
           options,
           out HashSet<string> proccessedFiles
          );

          proccessedFilesToMove.AddRange(proccessedFiles);
          builtTypes.AddRange(typesFromFolder);
        }
      }

      // get all loose types next:
      foreach ((string folder, ArchetypePorter porter) in Directory.GetDirectories(importsFolder)
          .Where(f => Universe.GetModData().PortersByArchetypeSubfolder.ContainsKey(Path.GetDirectoryName(f)))
          .Select(f => (folder: f, porter: Universe.GetModData().PortersByArchetypeSubfolder[Path.GetDirectoryName(f)]))
        ) {
        IEnumerable<TArchetype> typesFromFolder = ImportAndBuildNewArchetypesFromLooseFilesAndFolders(
         GetValidFlatFilesAndDirectoriesFromDirectory(folder),
         options,
         out HashSet<string> proccessedFiles
        );

        proccessedFilesToMove.AddRange(proccessedFiles);
        builtTypes.AddRange(typesFromFolder);
      }

      /// Package and save asset files and configs that are re-compiled for speed to the mod folder:
      foreach (TArchetype compiled in builtTypes) {
        SerializeArchetypeToModFiles(compiled, GetFolderFor(compiled));
      }

      // clean out the imports folder, moving items to the unprocessed or processed imports folder accordingly
      _cleanImportsFolder(proccessedFilesToMove.ToHashSet());
      return builtTypes;
    }

    void _cleanImportsFolder(HashSet<string> proccessedFilesToMove) {
      // move proccessed imports
      foreach(string fileToMove in proccessedFilesToMove) {
        File.Copy(fileToMove, fileToMove.Replace(ImportFolderName, ProcessedImportsFolderName), true);
      }

      // remove empty import directories
      _deleteEmptyDirectoriesRecusivelyUnder(
        Path.Combine(RootModsFolder, ImportFolderName)
      );

      // move remaining un-proccessed imports
      _copyDirectory(
        Path.Combine(RootModsFolder, ImportFolderName),
        Path.Combine(RootModsFolder, UnProccessedImportsFolderName),
        true
      );
    }

    /// <summary>
    /// Loose file import first searches for provided json config files (starting with _config.json) and obeys what they say to do.
    /// It then searches for provided folder names, and searches the folder contents for either a json for config, or the first Asset file to import and ignores all other files in these provided directories.
    /// It then goes though the originally provided loose Asset files (such as pngs) and tries to import each as it's own Archetype.
    /// </summary>
    public IEnumerable<TArchetype> ImportAndBuildNewArchetypesFromLooseFilesAndFolders(IEnumerable<string> externalFileAndFolderLocations, Dictionary<string, object> options, out HashSet<string> proccessedFiles) {
      List<TArchetype> builtTypes = new();
      List<string> configFiles = new();
      List<string> assetFiles = new();
      List<string> archetypeDirectories = new();

      List<string> allProcessedFiles = new();

      // for each file that doesn't start with `.`, or doesn't start with `_` and isn't named config.json.
      foreach (string providedItem in FilterOutInvalidFilenames(externalFileAndFolderLocations).Select(f => Path.GetFullPath(f))) {
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
      List<string> assetsToTryToBuildLooselyFrom = assetFiles
        .Select(f => Path.GetFullPath(f)).ToList();

      /// first, try to build all the configs.
      while (configFiles.Any()) {
        string currentConfig = configFiles.First();
        string currentConfigDirectory = Path.GetDirectoryName(currentConfig);
        List<string> assets = assetsToTryToBuildLooselyFrom.Except(currentConfig.AsSingleItemEnumerable()).ToList();

        // sort assets for this particular config.
        assets.Sort((x, y) => {
          string xDirectory = Path.GetDirectoryName(x);
          string yDirectory = Path.GetDirectoryName(y);
          if (yDirectory == currentConfigDirectory && xDirectory != currentConfigDirectory) {
            return 1;
          }
          else if (xDirectory == currentConfigDirectory && yDirectory != currentConfigDirectory) {
            return -1;
          }
          else
            return Path.GetFileName(x).CompareTo(Path.GetFileName(y));
        });

        // TODO: add a try here and record failed configs at the end.
        JObject config = JObject.Parse(File.ReadAllText(currentConfig));
        IEnumerable<TArchetype> assetResourceArchetypes =
          BuildLooselyFromConfig(config, assets.Prepend(currentConfig), options, out var processedFiles);

        if (assetResourceArchetypes.Any()) {
          _updateModData(assetResourceArchetypes.First().PackageKey, assetResourceArchetypes.First().ResourceKey, assetResourceArchetypes);
        }

        builtTypes.AddRange(assetResourceArchetypes);

        // remove proccessed files:
        configFiles.RemoveAt(0);
        if (processedFiles is not null) {
          proccessedFiles = processedFiles.Select(f => Path.GetFullPath(f)).ToHashSet();
          assetsToTryToBuildLooselyFrom = assetsToTryToBuildLooselyFrom.Except(processedFiles).ToList();
          configFiles = configFiles.Except(processedFiles).ToList();

          allProcessedFiles.AddRange(processedFiles);
        }
      }

      /// import directories:
      if (archetypeDirectories.Any()) {

        // modify options for directory style import:
        bool? currentSingleArchFolderOption
          = options.TryGetValue(FromSingleArchetypeFolderImportOptionsKey, out var existingOptionValue)
            ? (bool)existingOptionValue
            : null;
        options[FromSingleArchetypeFolderImportOptionsKey] = true;

        // import all directories
        while (archetypeDirectories.Any()) {
          string currentDirectory = archetypeDirectories.First();
          List<string> folderFiles = FilterOutInvalidFilenames(Directory.GetFiles(currentDirectory))
            .ToList();
          folderFiles.Sort(_byNameThenByFolder());

          IEnumerable<TArchetype> assetResourceArchetypes =
            BuildAllArchetypesFromSingleArchetypeFolder(
              currentDirectory,
              folderFiles,
              options,
              out var processedFiles
            );

          if (assetResourceArchetypes.Any()) {
            _updateModData(assetResourceArchetypes.First().PackageKey, assetResourceArchetypes.First().ResourceKey, assetResourceArchetypes);
          }

          builtTypes.AddRange(assetResourceArchetypes);

          // remove proccessed files:
          archetypeDirectories.RemoveAt(0);
          if (processedFiles is not null) {
            proccessedFiles = processedFiles.Select(f => Path.GetFullPath(f)).ToHashSet();
            assetsToTryToBuildLooselyFrom = assetsToTryToBuildLooselyFrom.Except(processedFiles).ToList();
            configFiles = configFiles.Except(processedFiles).ToList();

            allProcessedFiles.AddRange(processedFiles);
          }
        }

        // reset import options
        if (currentSingleArchFolderOption is not null) {
          options[FromSingleArchetypeFolderImportOptionsKey] = currentSingleArchFolderOption;
        }
        else options.Remove(FromSingleArchetypeFolderImportOptionsKey);
      }

      assetsToTryToBuildLooselyFrom.Sort(_byNameThenByFolder());
      // import loose files:
      while (assetsToTryToBuildLooselyFrom.Any()) {
        string currentAsset = assetsToTryToBuildLooselyFrom.First();

        IEnumerable<TArchetype> assetResourceArchetypes =
          BuildLooselyFromAssets(
            assetsToTryToBuildLooselyFrom,
            options,
            out var processedFiles
          );

        if (assetResourceArchetypes.Any()) {
          _updateModData(assetResourceArchetypes.First().PackageKey, assetResourceArchetypes.First().ResourceKey, assetResourceArchetypes);
        }

        builtTypes.AddRange(assetResourceArchetypes);

        // remove proccessed files:
        assetsToTryToBuildLooselyFrom.RemoveAt(0);
        if (processedFiles is not null) {
          proccessedFiles = processedFiles.Select(f => Path.GetFullPath(f)).ToHashSet();
          assetsToTryToBuildLooselyFrom = assetsToTryToBuildLooselyFrom.Except(processedFiles).ToList();

          allProcessedFiles.AddRange(processedFiles);
        }
      }

      proccessedFiles = allProcessedFiles.ToHashSet();
      return builtTypes;
    }

    /// <summary>
    /// Updates the universes mod package data.
    /// </summary>
    void _updateModData(string packageKey, string resourceKey, IEnumerable<TArchetype> builtTypes) {
      ModPorterContext modData = Universe.GetModData();

      if (modData.TryToGetModPackage(packageKey, out var existingPackage)) {
        existingPackage._addModAsset(ArchetypeBaseType, resourceKey, builtTypes);
      } else {
        modData._startNewModPackage(packageKey, resourceKey, builtTypes);
      }
    }

    #endregion

    #region Build Functions

    /// <summary>
    /// Used to build from a collection of files with a specific master config file.
    /// The master config file is also always provided as the first item in assetFiles.
    /// </summary>
    /// <param name="config">The provided config.json file</param>
    /// <param name="assetFiles">All of the asset file locations provided to try to build with</param>
    /// <param name="options">Import/Build options</param>
    /// <param name="processedFiles">All files touched/processed by the builder</param>
    /// <returns>All archetypes built from this config and asset files.</returns>
    protected abstract IEnumerable<TArchetype> BuildLooselyFromConfig(JObject config, IEnumerable<string> assetFiles, Dictionary<string, object> options, out IEnumerable<string> processedFiles);

    /// <summary>
    /// Used to build from a collection of files without specific master config file.
    /// The master config file is also always provided as the first item in assetFiles.
    /// </summary>
    /// <param name="assetFiles">All of the asset file locations provided to try to build with</param>
    /// <param name="options">Import/Build options</param>
    /// <param name="processedFiles">All files touched/processed by the builder</param>
    /// <returns>All archetypes built from this config and asset files.</returns>
    protected abstract IEnumerable<TArchetype> BuildLooselyFromAssets(IEnumerable<string> assetFiles, Dictionary<string, object> options, out IEnumerable<string> processedFiles);

    /// <summary>
    /// This processes this folder, and all sub folders, as "single archetype folders".
    /// This means it will search this (and each sub folder if recusive is enabled) for a single config file, or asset to build an archetype form, ignoring files wthat begin with . or _
    /// This just ignores directories with no valid items as well. 
    /// </summary>
    protected virtual IEnumerable<TArchetype> BuildAllArchetypesFromSingleArchetypeFolder(string folderLocation, IEnumerable<string> folderFiles, Dictionary<string, object> options, out IEnumerable<string> processedFiles, bool recursive = true) {
      List<string> folderItems = folderFiles.ToList();
      List<TArchetype> builtTypes = new();
      List<string> allProccessedFiles = new();

      // remove folders
      foreach (string providedItem in folderFiles) {
        if (recursive) {
          // if we're doing recursive, check each folder too
          FileAttributes attr = File.GetAttributes(providedItem);
          if (attr.HasFlag(FileAttributes.Directory)) {
            builtTypes.AddRange(BuildAllArchetypesFromSingleArchetypeFolder(folderLocation, folderFiles, options, out var processed, recursive));
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
        builtTypes.AddRange(BuildLooselyFromConfig(JObject.Parse(File.ReadAllText(configFile)), folderFiles.Except(configFile.AsSingleItemEnumerable()).Prepend(configFile), options, out var processed));
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
    /// Helper function for building the final archetypes using the correct ctor
    /// </summary>
    protected virtual IEnumerable<TArchetype> BuildArchetypeFromCompiledData(
      string resourceName,
      string packageName,
      string resourceKey,
      JObject config,
      Dictionary<string, object> importOptionsAndAssets,
      Universe universe
    ) {
      Type typeToBuild = ArchetypeBaseType;
      var porterConstructor = GetPorterConstructorForArchetypeType(typeToBuild);

      return ((TArchetype)(porterConstructor.Invoke(new object[] {
        resourceName, packageName, resourceKey, config, importOptionsAndAssets, universe
      }))).AsSingleItemEnumerable();
    }

    /// <summary>
    /// Can be used to help get the porter constructor for an archetype.
    /// </summary>
    protected static ConstructorInfo GetPorterConstructorForArchetypeType(Type typeToBuild) {
      ConstructorInfo porterConstructor = typeToBuild.GetConstructor(
        BindingFlags.Instance | BindingFlags.NonPublic,
        null,
        new Type[] {
          typeof(string),
          typeof(string),
          typeof(string),
          typeof(JObject),
          typeof(Dictionary<string, object>),
          typeof(Universe)
        },
        null
      );
      if (porterConstructor is null) {
        throw new Exception($"Archetype Type: {typeToBuild.FullName}, does not have a non-public constructor with the arguments: (string)resourceName, (string)packageName, (string)resourceKey, (JObject)cofig, (Dictionary<string, object>)importOptionsAndAssets, (Universe)universe.");
      }

      return porterConstructor;
    }

    #endregion

    void _cacheArchetype(TArchetype archetype, string packageName = null) {
      _cachedResources.Add(archetype.ResourceKey, archetype);
      if (_cachedResourcesByPackage.TryGetValue(packageName ?? "", out var existingSet)) {
        existingSet.Add(archetype.ResourceKey, archetype);
      }
      else if (!string.IsNullOrWhiteSpace(packageName)) {
        _cachedResourcesByPackage.Add(packageName, new() {
          {
            archetype.ResourceKey,
            archetype
          }
        });
      }
    }

    #endregion

    #region Export

    /// <summary>
    /// Serialize this archetype to a set of files in the mod folder.
    /// </summary>
    /// <param name="archetype">The archetype to serialize into a file or files</param>
    /// <param name="archetypeModFolderPath">The root path to save files to for this archetype</param>
    /// <returns>The newly serialized file's locations</returns>
    protected abstract string[] SerializeArchetypeToModFiles(TArchetype archetype, string archetypeModFolderPath);

    ///<summary><inheritdoc/></summary>
    public string[] SerializeArchetypeToModFolder(TArchetype archetype)
      => SerializeArchetypeToModFiles(
        archetype,
        GetFolderFor(archetype)
      );

    #endregion

    #region Update and Delete

    #endregion

    #region Utility

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

    static void _deleteEmptyDirectoriesRecusivelyUnder(string parentDir) {
      // Get information about the source directory
      var dir = new DirectoryInfo(parentDir);

      // Check if the source directory exists
      if(!dir.Exists)
        throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

      // Cache directories before we start copying
      DirectoryInfo[] dirs = dir.GetDirectories();

      // If recursive and copying subdirectories, recursively call this method
      foreach(DirectoryInfo subDir in dirs) {
        _deleteEmptyDirectoriesRecusivelyUnder(subDir.FullName);
        if (!subDir.GetFiles().Any() || !subDir.GetDirectories().Any()) {
          subDir.Delete();
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

    #endregion

    #region IPorter

    IEnumerable<Archetype> IArchetypePorter.ImportAndBuildNewArchetypesFromLooseFilesAndFolders(string[] externalFileAndFolderLocations, Dictionary<string, object> options, out HashSet<string> processedFiles)
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
      => ImportIndividualArchetypeFromModFolder(resourceKey, options);

    Archetype IArchetypePorter.TryToFindArchetypeAndLoadFromModFolder(string resourceKey, Dictionary<string, object> options)
      => TryToFindAndImportIndividualArchetypeFromModFolder(resourceKey, options);

    string[] IArchetypePorter.SerializeArchetypeToModFolder(Archetype archetype)
      => SerializeArchetypeToModFolder((TArchetype)archetype);

    string IArchetypePorter.GetFolderFor(Archetype portableArchetype)
      => GetFolderFor((TArchetype)portableArchetype);

    #endregion
  }
}

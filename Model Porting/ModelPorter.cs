using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Meep.Tech.Data.IO {
  
  /// <summary>
  /// Used to in and export models to the data folder, or to mod packages.
  /// </summary>
  public abstract class ModelPorter {
    
    /// <summary>
    /// The universe this is for
    /// </summary>
    public Universe Universe {
      get;
      internal set;
    }

    /// <summary>
    /// The base type of the model
    /// </summary>
    public abstract System.Type ModelBaseType { 
      get;
    }

    /// <summary>
    /// Get the root folder to save to for models of this type.
    /// </summary>
    public abstract string GetSaveToRootFolder(ModPackage modPackage = null);

    /// <summary>
    /// Try to load an item by key
    /// </summary>
    public bool TryToLoadByKey(string key, out IModel model, ModPackage fromModPackage = null)
      => (model = TryToLoadByKey(key, fromModPackage)) != null;

    /// <summary>
    /// Try to load an item by key
    /// </summary>
    public IModel TryToLoadByKey(string key, ModPackage fromModPackage = null) {
      var modelFolder = Path.Combine(GetSaveToRootFolder(fromModPackage), key);
      var modelMainCofig = Directory.GetFiles(modelFolder)
        .Where(f => !f.StartsWith("_") || f.StartsWith("."))
        .OrderBy(f => f)
        .FirstOrDefault(f => Path.GetExtension(f).ToLower() == ModelMetaData.MainDataFileExtension);
      
      if (modelMainCofig is not null) {
        return _loadFromDataFile(modelMainCofig);
      }

      return null;
    }
    /// <summary>
    /// load an item by key
    /// </summary>
    public IModel LoadByKey(string key, ModPackage fromModPackage = null) {
      var modelFolder = Path.Combine(GetSaveToRootFolder(fromModPackage), key);
      var modelMainCofig = Directory.GetFiles(modelFolder)
        .Where(f => !f.StartsWith("_") || f.StartsWith("."))
        .OrderBy(f => f)
        .First(f => Path.GetExtension(f).ToLower() == ModelMetaData.MainDataFileExtension);

      return _loadFromDataFile(modelMainCofig);
    }

    /// <summary>
    /// Try to load an item by key
    /// </summary>
    internal abstract IModel _loadFromDataFile(string modelMainCofig);
  }

  /// <summary>
  /// Used to in and export models
  /// </summary>
  public class ModelPorter<TModel> : ModelPorter where TModel : class, IModel {

    /// <summary>
    /// The instance of this model porter type from the default universe.
    /// </summary>
    public static ModelPorter<TModel> DefaultInstance
      => (ModelPorter<TModel>)Models.DefaultUniverse.GetModData().ModelPorters[typeof(TModel)];

    /// <summary>
    /// The base type this is in charge of porting.
    /// </summary>
    public override Type ModelBaseType 
      => typeof(TModel);

    /// <summary>
    /// Get the root folder to save models of this type to.
    /// ex: '_items'
    /// </summary>
    protected virtual string SaveDataRootFolderName { 
      get; 
    }

    /// <summary>
    /// Used to get the unique folder name to save a model to.
    /// </summary>
    protected virtual Func<TModel, string> GetSubFolderName {
      get;
    }

    /// <summary>
    /// Used to get the name of the unique config (data.json).
    /// This file name is used as the display name in metadata lists.
    /// </summary>
    protected virtual Func<TModel, string> GetMainConfigFileName {
      get;
    }

    /// <summary>
    /// Used to get a metadata icon from a filename.
    /// This runs on every file when skimming metadata from a folder.
    /// </summary>
    protected virtual Func<string, object> TryToGetMetadataIcon {
      get;
    }

    /// <summary>
    /// Can be overriden to save extra data file data.
    /// </summary>
    /// <returns>Returns the names of any files created/updated/saved to</returns>
    protected virtual Func<string, TModel, IEnumerable<string>> OnSaveDataFiles { 
      get; 
    }

    /// <summary>
    /// Make a new model porter of the given type.
    /// </summary>
    /// <param name="universe"></param>
    public ModelPorter(
      Universe universe,
      string saveDataRootFolderNameForModelType,
      Func<TModel, string> getSubFolderName,
      Func<TModel, string> getMainConfigFileName,
      Func<string, TModel, IEnumerable<string>> onSaveDataFiles = null,
      Func<string, object> tryToGetMetadataIconFromFilenameLogic = null
    ) {
      Universe = universe;
      SaveDataRootFolderName = saveDataRootFolderNameForModelType;
      GetSubFolderName = getSubFolderName;
      GetMainConfigFileName = getMainConfigFileName;
      OnSaveDataFiles = onSaveDataFiles;
      TryToGetMetadataIcon = tryToGetMetadataIconFromFilenameLogic;
    }

    ///<summary>
    /// Try to load a model by key
    /// </summary>
    public new TModel TryToLoadByKey(string key, ModPackage fromModPackage = null) {
      if (typeof(ICached).IsAssignableFrom(typeof(TModel))) {
        TModel existing;
        if ((existing = ICached.TryToGetFromCache(key) as TModel) != null) {
          return existing;
        }
      }

      return (TModel)base.TryToLoadByKey(key, fromModPackage);
    }

    ///<summary>
    /// Try to load a model by key
    /// </summary>
    public bool TryToLoadByKey(string key, out TModel model, ModPackage fromModPackage = null) 
      => (model = TryToLoadByKey(key, fromModPackage)) != null;

    /// <summary>
    /// Get metadata for all saved items of the given model type.
    /// </summary>
    public IEnumerable<ModelMetaData<TModel>> GetMetadataForAll(ModPackage fromModPackage = null) {
      List<ModelMetaData<TModel>> metaDatas = new();
      foreach (string modelDataFolder in Directory.GetDirectories(GetSaveToRootFolder())) {
        var metaData =
          LoadMetadataFromModelFolder(modelDataFolder);
        if (metaData != null) {
          metaDatas.Add(metaData);
        }
      }

      return metaDatas;
    }

    /// <summary>
    /// Get an item by key. This checks the cache first
    /// </summary>
    public new TModel LoadByKey(string key, ModPackage fromModPackage = null) {
      if (typeof(ICached).IsAssignableFrom(typeof(TModel))) {
        return (TModel)ICached.GetFromCache(key);
      }

      return (TModel)base.LoadByKey(key, fromModPackage);
    }

    /// <summary>
    /// Load an item's metadata from it's model folder.
    /// </summary>
    public ModelMetaData<TModel> LoadMetadataFromModelFolder(string modelDataFolder) {
      string configFile = null;
      object icon = null;
      foreach (var file in Directory.GetFiles(modelDataFolder).Where(f => !f.StartsWith("_") || f.StartsWith(".")).OrderBy(x => x)) {
        if (configFile is not null && icon is not null) {
          break;
        }
        if (configFile is null && Path.GetFileName(file).ToLower().EndsWith(ModelMetaData.MainDataFileExtension)) {
          configFile = file;
          if (icon is not null) {
            break;
          }
        }
        if (icon is null) {
          icon = TryToGetMetadataIcon?.Invoke(file);
        }
      }

      // if we found a character config in this folder, make a metadata entry for it.
      if (configFile is not null) {
        string name = Path.GetFileNameWithoutExtension(configFile);
        DateTime lastEditedFileData = Directory.GetLastWriteTime(modelDataFolder);

        return MakeMetadata(name, modelDataFolder, lastEditedFileData, icon);
      }

      return null;
    }

    /// <summary>
    /// Can be overriden to make a different kind of metadata.
    /// </summary>
    protected virtual ModelMetaData<TModel> MakeMetadata(string name,string modelDataFolder, DateTime lastEditedFileData, object icon = null, ModPackage modPackage = null) 
      => new(name, modelDataFolder, lastEditedFileData, icon);

    ///<summary><inheritdoc/></summary>
    public override string GetSaveToRootFolder(ModPackage modPackage = null) => Path.Combine(
      Universe.GetModData().RootDataFolder,
      SaveDataRootFolderName
    );

    /// <summary>
    /// Get the folder this model should be saved to.
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    public string GetSaveToFolder(TModel model, ModPackage modPackage = null)
      => Path.Combine(GetSaveToRootFolder(modPackage), GetSubFolderName(model));

    /// <summary>
    /// Used to save a model to the data folder.
    /// This clears the folder and overwrites any existing data files. non critical Sub-directories are left ignored.
    /// </summary>
    /// <param name="toModPackage">(optional) the mod package to save the item to, if it's for a mod and not just save data</param>
    /// <returns>The metadata for the saved model</returns>
    public ModelMetaData<TModel> Save(TModel model, ModPackage toModPackage = null) {
      JObject data = model.ToJson();
      string name = GetMainConfigFileName(model);
      string saveToFolder = GetSaveToFolder(model, toModPackage);

      // clear existing loose files in the save directory for this model:
      if (Directory.Exists(saveToFolder)) {
        foreach(string file in ArchetypePorter.FilterOutInvalidFilenames(Directory.GetFiles(saveToFolder), false)) {
          File.Delete(file);
        }
      } else {
        Directory.CreateDirectory(saveToFolder);
      }

      var metadata = MakeMetadata(name, saveToFolder, DateTime.Now, toModPackage);

      File.WriteAllText(metadata.MainDataFileLocation, data.ToString());
      OnSaveDataFiles?.Invoke(saveToFolder, model);

      return metadata;
    }

    /// <summary>
    /// Delete the data for the given model with the given id.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="toModPackage"></param>
    public void Delete(string id, ModPackage toModPackage = null) {
      throw new NotImplementedException();
    }

    internal override IModel _loadFromDataFile(string modelMainCofig) {
      JObject json = JObject.Parse(File.ReadAllText(modelMainCofig));
      return IModel.FromJson(json, typeof(TModel));
    }
  }
}

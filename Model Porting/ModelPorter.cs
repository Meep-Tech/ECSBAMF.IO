using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Meep.Tech.Data.IO {

  /// <summary>
  /// Used to in and export models.
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
    public abstract string GetSaveToRootFolder();

    /// <summary>
    /// Try to load an item by key
    /// </summary>
    public bool TryToLoadByKey(string key, out IModel model)
      => (model = TryToLoadByKey(key)) != null;

    /// <summary>
    /// Try to load an item by key
    /// </summary>
    public IModel TryToLoadByKey(string key) {
      var modelFolder = Path.Combine(GetSaveToRootFolder(), key);
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
    /// Try to load an item by key
    /// </summary>
    internal abstract IModel _loadFromDataFile(string modelMainCofig);
  }

  /// <summary>
  /// Used to in and export models
  /// </summary>
  /// <typeparam name="TModel"></typeparam>
  public class ModelPorter<TModel> : ModelPorter where TModel : IModel {

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
    /// Make a new model porter of the given type.
    /// </summary>
    /// <param name="universe"></param>
    public ModelPorter(Universe universe) {
      Universe = universe;
    }

    ///<summary>
    /// Try to load a model by key
    /// </summary>
    public new TModel TryToLoadByKey(string key) 
      => (TModel)base.TryToLoadByKey(key);

    ///<summary>
    /// Try to load a model by key
    /// </summary>
    public bool TryToLoadByKey(string key, out TModel model) 
      => (model = TryToLoadByKey(key)) != null;

    /// <summary>
    /// Get metadata for all saved items of the given model type.
    /// </summary>
    public IEnumerable<ModelMetaData<TModel>> GetMetadataForAll() {
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
          icon = _getDefaultArchetype().TryToGetMetadataIcon(file);
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
    protected virtual ModelMetaData<TModel> MakeMetadata(string name,string modelDataFolder, DateTime lastEditedFileData, object icon = null) 
      => new(name, modelDataFolder, lastEditedFileData, icon);

    ///<summary><inheritdoc/></summary>
    public override string GetSaveToRootFolder() => Path.Combine(
      Universe.GetModData().RootDataFolder,
      _getDefaultArchetype().SaveDataRootFolderNameForModelType
    );

    /// <summary>
    /// Get the folder this model should be saved to.
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    public string GetSaveToFolder(TModel model)
      => Path.Combine(GetSaveToRootFolder(), _getDefaultArchetype().GetSubFolderName(model));

    /// <summary>
    /// Used to save a model to the data folder.
    /// This clears the folder and overwrites any existing data files. non critical Sub-directories are left ignored.
    /// </summary>
    /// <returns>The metadata for the saved model</returns>
    public ModelMetaData<TModel> Save(TModel model) {
      JObject data = model.ToJson();
      IHasPortableModel<TModel> type = _getDefaultArchetype();
      string name = type.GetMainConfigFileName(model);
      string saveToFolder = GetSaveToFolder(model);

      // clear existing loose files in the save directory for this model:
      if (Directory.Exists(saveToFolder)) {
        foreach(string file in ArchetypePorter.FilterOutInvalidFilenames(Directory.GetFiles(saveToFolder), false)) {
          File.Delete(file);
        }
      } else {
        Directory.CreateDirectory(saveToFolder);
      }

      var metadata = MakeMetadata(name, saveToFolder, DateTime.Now);

      File.WriteAllText(metadata.MainDataFileLocation, data.ToString());
      type.SaveExtraDataFiles(saveToFolder);

      return metadata;
    }

    internal override IModel _loadFromDataFile(string modelMainCofig) {
      JObject json = JObject.Parse(File.ReadAllText(modelMainCofig));
      return IModel.FromJson(json, typeof(TModel));
    }

    IHasPortableModel<TModel> _getDefaultArchetype()
      => (Archetypes.DefaultUniverse.Archetypes.GetDefaultForModelOfType<TModel>()
        as IHasPortableModel<TModel>) 
          ?? throw new ArgumentException($"Archetype: {Archetypes.DefaultUniverse.Archetypes.GetDefaultForModelOfType<TModel>()?.ToString() ?? "ERROR-NODEFAULTARCHETYPEFOUD"}, " 
            + $"which is the default for the model: {typeof(TModel).FullName}, does not inherit from: {typeof(IHasPortableModel<TModel>).FullName}, so it's model caot be ported.");
  }
}

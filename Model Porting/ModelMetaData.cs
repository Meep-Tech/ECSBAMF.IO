using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Meep.Tech.Data.IO {

  /// <summary>
  /// Some metadata that can be skimmed from saved model files without opening the whole data.json
  /// </summary>
  public class ModelMetaData {

    /// <summary>
    /// The file extension expexcted on the main data file for a model
    /// </summary>
    public const string MainDataFileExtension = ".data.json";

    /// <summary>
    /// The location of the folder this model's data is saved to.
    /// </summary>
    public string Folder {
      get;
    }

    /// <summary>
    /// The location of the folder this model's data is saved to.
    /// </summary>
    public string MainDataFileLocation
      => _mainDataFileLocation ??= Path.Combine(Folder, MainDataFileName);
    string _mainDataFileLocation;

    /// <summary>
    /// The location of the folder this model's data is saved to.
    /// </summary>
    public string MainDataFileName
      => _mainDataFileName ??= Path.ChangeExtension(Name, MainDataFileExtension);
    string _mainDataFileName;

    /// <summary>
    /// The name of the data item.
    /// Taken from the name of it's .data.json file.
    /// </summary>
    public string Name {
      get;
    }

    /// <summary>
    /// When this was last updated.
    /// </summary>
    public DateTime LastUpdated {
      get;
    }

    /// <summary>
    /// The key for the data. 
    /// Taken from the folder name
    /// </summary>
    public string Key {
      get;
    }

    /// <summary>
    /// the default icon. named _icon.png.
    /// For unityengine, this is a Sprite.
    /// </summary>
    public object Icon {
      get;
    }

    internal ModelMetaData(string name, string folder, DateTime lastUpdated, object icon) {
      Name = name;
      Folder = folder;
      Key = Path.GetFileNameWithoutExtension(folder);
      LastUpdated = lastUpdated;
      Icon = icon;
    }
  }

  ///<summary><inheritdoc/></summary>
  public class ModelMetaData<TModel> : ModelMetaData where TModel : IModel {

    /// <summary>
    /// Lazy loaded link to the model
    /// </summary>
    public TModel Model
      => _model ??= _load();
    TModel _model;

    /// <summary>
    /// Used to make new metadata
    /// </summary>
    internal protected ModelMetaData(string name, string folder, DateTime lastUpdated, object sprite = null)
      : base(name, folder, lastUpdated, sprite) { }

    /// <summary>
    /// Overrideable function to do other stuff on load.
    /// </summary>
    protected virtual void OnLoad(JObject json, string folder) {}

    TModel _load() {
      string expectedDataFile = MainDataFileName;
      string dataFile = null;
      List<string> jsonDataFiles = new();
      foreach (var file in Directory.GetFiles(Folder).Where(f => !f.StartsWith("_") || f.StartsWith(".")).Where(f => f.ToLower().EndsWith(MainDataFileExtension))) {
        if (file.ToLower() != expectedDataFile) {
          jsonDataFiles.Add(file);
        } else {
          dataFile = file;
          break;
        }
      }
      if (dataFile is null) {
        dataFile = jsonDataFiles.First();
      }

      JObject json = JObject.Parse(File.ReadAllText(dataFile));
      OnLoad(json, Folder);
      return (TModel)IModel.FromJson(json, typeof(TModel));
    }
  }
}

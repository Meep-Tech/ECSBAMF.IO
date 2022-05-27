using System.Collections.Generic;
using System.Linq;

namespace Meep.Tech.Data.IO {

  /// <summary>
  /// Attached to an archetype that produced a model that can be in/exporte.
  /// </summary>
  public interface IHasPortableModel<TModel> where TModel : IModel {

    /// <summary>
    /// The root folder to save models of this type to.
    /// </summary>
    string SaveDataRootFolderNameForModelType {
      get;
    }

    /// <summary>
    /// Used to get the unique folder name to save a model to.
    /// </summary>
    public string GetSubFolderName(TModel model);

    /// <summary>
    /// Used to get the name of the unique config (data.json).
    /// This file name is used as the display name in metadata lists.
    /// </summary>
    public string GetMainConfigFileName(TModel model);

    /// <summary>
    /// Used to get a metadata icon from a filename.
    /// This runs on every file when skimming metadata from a folder.
    /// </summary>
    public object TryToGetMetadataIcon(string fileName)
      => null;

    /// <summary>
    /// Can be overriden to save extra data file data.
    /// </summary>
    /// <returns>Returns the names of any files created/updated/saved to</returns>
    public IEnumerable<string> SaveExtraDataFiles(string modelSaveToFolder)
      => Enumerable.Empty<string>();
  }
}

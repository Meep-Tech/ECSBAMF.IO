using Meep.Tech.Data.Configuration;
using Newtonsoft.Json.Linq;

namespace Meep.Tech.Data.IO {

  /// <summary>
  /// Indicates a model does something on save.
  /// This can be used to save dependencies.
  /// </summary>
  public interface IDoOnSave : IUnique, ITrait<IDoOnSave> {

    string ITrait<IDoOnSave>.TraitName
      => $"Does Something On Save";

    string ITrait<IDoOnSave>.TraitDescription
      => $"This model will execute code when it is saved using a ModelPorter";

    /// <summary>
    /// Execute code when this model is saved.
    /// </summary>
    protected internal void OnSave(JObject serializedModelData, ModelMetaData metaData, string saveToFolder, ModPackage toModPackage = null);
  }
}

using System.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace Meep.Tech.Data.IO {

  /// <summary>
  /// An alternative to ModPorterContext that automatically creates a porter for all model types.
  /// </summary>
  public class FullModelCoverageModImportContext : Universe.ExtraContext {
    ModPorterContext _baseModImportSettings;
    Dictionary<System.Type, ModelPorter> _porters 
      = new();

    /// <summary>
    /// Make a full coverage model porter context from the base mod context.
    /// All values from the base context are carried over and applied as well.
    /// </summary>
    public FullModelCoverageModImportContext([NotNull] ModPorterContext baseModImportSettings) {
      _baseModImportSettings = baseModImportSettings;
    }

    ///<summary><inheritdoc/></summary>
    protected override void OnModelTypeRegistered(Type modelType, IModel defaultModel) {
      if(!_porters.TryGetValue(modelType.GetModelBaseType(), out _)) {
        ModelPorter porter;

        System.Type genericPorterType = typeof(ModelPorter<>)
          .MakeGenericType(modelType.GetModelBaseType());

        porter = (ModelPorter)Activator.CreateInstance(genericPorterType);

        _porters[modelType.GetModelBaseType()] = porter;
      }
    }

    ///<summary><inheritdoc/></summary>
    protected override void OnAllTypesInitializationComplete() {
    string rootFolder = _baseModImportSettings.RootDataFolder;
      // or possibly
      DirectoryInfo parentDir = Directory.GetParent(rootFolder.EndsWith("\\") ? rootFolder : string.Concat(rootFolder, "\\"));

      // The result is available here
      Universe.AddModImportContext(new(
        Universe,
        parentDir.Parent.FullName,
        _baseModImportSettings.ArchetypePorters.Values,
        _baseModImportSettings.ModelPorters.Values.Concat(_porters.Values)
      ));

      // clear memory
      _baseModImportSettings = null;
      _porters = null;
    }
  }
}

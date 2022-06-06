using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Meep.Tech.Collections.Generic;
using Meep.Tech.Data.Reflection;

namespace Meep.Tech.Data.IO {

  /// <summary>
  /// Settings and data for mod porters.
  /// </summary>
  public class ModPorterContext : Universe.ExtraContext {

    /// <summary>
    /// The base mod folder name
    /// </summary>
    public const string ModFolderName
      = "mods";

    /// <summary>
    /// The default folder to save model data to
    /// </summary>
    public const string DataFolderName
      = "data";

    /// <summary>
    /// The root mod folder.
    /// </summary>
    public string RootModsFolder {
      get;
    }

    /// <summary>
    /// The root data folder.
    /// </summary>
    public string RootDataFolder {
      get;
    }

    /// <summary>
    /// The porters for archetypes.
    /// </summary>
    public IReadOnlyDictionary<System.Type, ArchetypePorter> ArchetypePorters
      => _archetypePorters; readonly Dictionary<System.Type, ArchetypePorter> _archetypePorters
        = new();

    /// <summary>
    /// The porters for models.
    /// </summary>
    public IReadOnlyDictionary<System.Type, ModelPorter> ModelPorters
      => _modelPorters; readonly Dictionary<System.Type, ModelPorter> _modelPorters
        = new();

    /// <summary>
    /// The porters.
    /// </summary>
    public IReadOnlyDictionary<string, ArchetypePorter> PortersByArchetypeSubfolder
      => _portersByArchetypeSubFolder; readonly Dictionary<string, ArchetypePorter> _portersByArchetypeSubFolder
        = new();

    /// <summary>
    /// The porters.
    /// </summary>
    public IReadOnlyDictionary<string, ModelPorter> PortersByModelSubfolder
      => _portersByModelSubFolder; Dictionary<string, ModelPorter> _portersByModelSubFolder
        = new();

    /// <summary>
    /// All the mods that were imported using porters.
    /// </summary>
    public IReadOnlyDictionary<string, ModPackage> ImportedMods
      => _importedMods; readonly Dictionary<string, ModPackage> _importedMods
        = new();

    /// <summary>
    /// The universe for this context.
    /// </summary>
    public Universe Universe {
      get;
    }

    /// <summary>
    /// Make new mod porter settings to add to a universe.
    /// </summary>
    /// <param name="rootApplicationPersistentDataFolder">The directory to put the mods and data folders inside of</param>
    public ModPorterContext(Universe universe, string rootApplicationPersistentDataFolder, IEnumerable<ArchetypePorter> archetypePorters = null, IEnumerable<ModelPorter> modelPorters = null) {
      archetypePorters ??= Enumerable.Empty<ArchetypePorter>();
      modelPorters ??= Enumerable.Empty<ModelPorter>();

      Universe = universe;
      RootModsFolder = Path.Combine(rootApplicationPersistentDataFolder, ModFolderName);
      RootDataFolder = Path.Combine(rootApplicationPersistentDataFolder, DataFolderName);

      archetypePorters.ForEach(p => p._universe = universe);
      _archetypePorters = archetypePorters.ToDictionary(p => p.ArchetypeBaseType);
      _portersByArchetypeSubFolder = archetypePorters.ToDictionary(p => p.SubFolderName);

      modelPorters.ForEach(p => p.Universe = universe);
      _modelPorters = modelPorters.ToDictionary(p => p.ModelBaseType);
    }

    ///<summary><inheritdoc/></summary>
    protected override void OnLoaderFinalize() {
      _portersByModelSubFolder = _modelPorters.Values.ToDictionary(p => p.GetSaveToRootFolder());
    }

    ///<summary><inheritdoc/></summary>
    protected override void OnUnload(Archetype archetype) {
      // remove from mod assets.
      if (archetype is IPortableArchetype portableType) {
        GetModPackage(portableType.PackageKey)
          ._removeModAsset(portableType);
      }
    }

    #region Get Porters

    /// <summary>
    /// Get the desired porter.
    /// </summary>
    public ArchetypePorter<TArchetype> GetArchetypePorter<TArchetype>()
      where TArchetype : Archetype, IPortableArchetype
        => (ArchetypePorter<TArchetype>)(_archetypePorters.TryGetValue(typeof(TArchetype), out var found)
          ? found
          : (_archetypePorters[typeof(TArchetype)] = _archetypePorters
            .OrderByDescending(porter => porter.Key.GetDepthOfInheritance())
            .First(porter => porter.Value.ArchetypeBaseType.IsAssignableFrom(typeof(TArchetype))).Value));

    /// <summary>
    /// Try to get the desired porter.
    /// Null on none found.
    /// </summary>
    public ArchetypePorter<TArchetype> TryToGetArchetypePorter<TArchetype>()
      where TArchetype : Archetype, IPortableArchetype
        => TryToGetPorterFor<TArchetype>(out var porter)
          ? porter
          : null;

    /// <summary>
    /// Try to get the desired porter.
    /// Null on none found.
    /// </summary>
    public bool TryToGetPorterFor<TArchetype>(out ArchetypePorter<TArchetype> porter)
      where TArchetype : Archetype, IPortableArchetype {
      if (_archetypePorters.TryGetValue(typeof(TArchetype), out var found)) {
        porter = (ArchetypePorter<TArchetype>)found;
        return true;
      }

      porter = (ArchetypePorter<TArchetype>)_archetypePorters
        .OrderByDescending(porter => porter.Key.GetDepthOfInheritance())
        .FirstOrDefault(porter => porter.Value.ArchetypeBaseType.IsAssignableFrom(typeof(TArchetype))).Value;

      if (porter != null) {
        _archetypePorters[typeof(TArchetype)]
          = porter;
      }

      return porter != null;
    }

    /// <summary>
    /// Get the desired porter.
    /// </summary>
    public ModelPorter<TModel> GetModelPorter<TModel>()
      where TModel : IModel
        => (ModelPorter<TModel>)(_modelPorters.TryGetValue(typeof(TModel), out var found)
          ? found
          : (_modelPorters[typeof(TModel)] = _modelPorters
              .OrderByDescending(porter => porter.Key.GetDepthOfInheritance())
              .First(porter => porter.Value.ModelBaseType.IsAssignableFrom(typeof(TModel))).Value));

    /// <summary>
    /// Try to get the desired porter.
    /// Null on none found.
    /// </summary>
    public ModelPorter<TModel> TryToGetModelPorter<TModel>()
      where TModel : IModel
        => TryToGetPorterFor<TModel>(out var porter)
          ? porter
          : null;

    /// <summary>
    /// Try to get the desired porter.
    /// Null on none found.
    /// </summary>
    public ModelPorter TryToGetModelPorter(System.Type modelType)
      => TryToGetModelPorter(modelType, out var porter)
        ? porter
        : null;

    /// <summary>
    /// Try to get the desired porter.
    /// Null on none found.
    /// </summary>
    public bool TryToGetModelPorter(System.Type modelType, out ModelPorter porter) {
      if (_modelPorters.TryGetValue(modelType, out var found)) {
        porter = found;
        return true;
      }

      porter = _modelPorters
        .OrderByDescending(porter => porter.Key.GetDepthOfInheritance())
        .FirstOrDefault(porter => porter.Value.ModelBaseType.IsAssignableFrom(modelType)).Value;

      if (porter != null) {
        _modelPorters[modelType]
          = porter;
      }

      return porter != null;
    }

    /// <summary>
    /// Try to get the desired porter.
    /// Null on none found.
    /// </summary>
    public bool TryToGetPorterFor<TModel>(out ModelPorter<TModel> porter)
      where TModel : IModel 
    {
      if (_modelPorters.TryGetValue(typeof(TModel), out var found)) {
        porter = (ModelPorter<TModel>)found;
        return true;
      }

      porter = (ModelPorter<TModel>)_modelPorters
        .OrderByDescending(porter => porter.Key.GetDepthOfInheritance())
        .FirstOrDefault(porter => porter.Value.ModelBaseType.IsAssignableFrom(typeof(TModel))).Value;

      if (porter != null) {
        _modelPorters[typeof(TModel)]
          = porter;
      }

      return porter != null;
    }

    #endregion

    #region Get Modpack

    /// <summary>
    /// Get a modpackage by key.
    /// </summary>
    public ModPackage GetModPackage(string packageOrResourceKey) {
      if (packageOrResourceKey.Contains("::")) {
        return _importedMods[packageOrResourceKey.Split("::").First()];
      }

      return _importedMods[packageOrResourceKey];
    }

    /// <summary>
    /// Get a modpackage by key.
    /// </summary>
    public ModPackage TryToGetModPackage(string packageOrResourceKey) {
      if (packageOrResourceKey.Contains("::")) {
        return _importedMods.TryGetValue(packageOrResourceKey.Split("::").First(), out var foundA)
          ? foundA
          : null;
      }

      return _importedMods.TryGetValue(packageOrResourceKey, out var found)
          ? found
          : null;
    }

    /// <summary>
    /// Get a modpackage by key.
    /// </summary>
    public bool TryToGetModPackage(string packageOrResourceKey, out ModPackage modPackage) {
      if (packageOrResourceKey.Contains("::")) {
        return _importedMods.TryGetValue(packageOrResourceKey.Split("::").First(), out modPackage);
      }

      return _importedMods.TryGetValue(packageOrResourceKey, out modPackage);
    }

    #endregion

    #region Get Resources

    /// <summary>
    /// Get the archetypes for the given resource key and type.
    /// </summary>
    public IEnumerable<IPortableArchetype> GetResources(Type assetArchetypeType, string resourceKey)
      => GetModPackage(resourceKey).Get(assetArchetypeType, resourceKey);

    /// <summary>
    /// Try to get the archetypes for the given resource key and type.
    /// Returns empty enumerable on failure to find.
    /// </summary>
    public IEnumerable<IPortableArchetype> TryToGetResources(Type assetArchetypeType, string resourceKey)
      => GetModPackage(resourceKey).TryToGet(assetArchetypeType, resourceKey);

    /// <summary>
    /// Try to get the archetypes for the given resource key and type.
    /// provides an empty enumerable on failure to find.
    /// </summary>
    public bool TryToGetResources(Type assetArchetypeType, string resourceKey, out IEnumerable<IPortableArchetype> foundArchetypes)
      => GetModPackage(resourceKey).TryToGet(assetArchetypeType, resourceKey, out foundArchetypes);

    /// <summary>
    /// Get the archetypes for the given resource key and type.
    /// </summary>
    public IEnumerable<TArchetype> GetResources<TArchetype>(string resourceKey)
      where TArchetype: Archetype, IPortableArchetype
        => GetModPackage(resourceKey).Get<TArchetype>(resourceKey);

    /// <summary>
    /// Try to get the archetypes for the given resource key and type.
    /// Returns empty enumerable on failure to find.
    /// </summary>
    public IEnumerable<TArchetype> TryToGetResources<TArchetype>(string resourceKey)
      where TArchetype : Archetype, IPortableArchetype
        => GetModPackage(resourceKey).TryToGet<TArchetype>(resourceKey);

    /// <summary>
    /// Try to get the archetypes for the given resource key and type.
    /// provides an empty enumerable on failure to find.
    /// </summary>
    public bool TryToGetResources<TArchetype>(string resourceKey, out IEnumerable<TArchetype> foundArchetypes)
      where TArchetype : Archetype, IPortableArchetype
        => GetModPackage(resourceKey).TryToGet(resourceKey, out foundArchetypes);

    /// <summary>
    /// Try to find any archetypes with the given resource key, reguardless of their base type.
    /// Returns empty if none found.
    /// </summary>
    public IEnumerable<IPortableArchetype> TryToGetResourcesOfUnknownType(string resourceKey)
        => GetModPackage(resourceKey).TryToGetResourcesOfUnknownType(resourceKey);

    /// <summary>
    /// Try to find any archetypes with the given resource key, reguardless of their base type.
    /// provides empty if none found.
    /// </summary>
    public bool TryToGetResourcesOfUnknownType(string resourceKey, out IEnumerable<IPortableArchetype> foundArchetypes)
        => GetModPackage(resourceKey).TryToGetResourcesOfUnknownType(resourceKey, out foundArchetypes);

    #endregion

    internal void _startNewModPackage<TArchetype>(
      string packageKey,
      string importedResourceKey,
      IEnumerable<TArchetype> importedTypes
    ) where TArchetype : Archetype, IPortableArchetype {
      var mod = new ModPackage(packageKey);
      mod._addModAsset(typeof(TArchetype).TryToGetAsArchetype().BaseArchetype, importedResourceKey, importedTypes);
      _importedMods.Add(packageKey, mod);
    }
  }

  /// <summary>
  /// Helpers to get mods and resources from the universe
  /// </summary>
  public static class ModContextExtensions {

    /// <summary>
    /// Get the full mod data from the given universe.
    /// </summary>
    public static ModPorterContext GetModData(this Universe universe)
      => universe.GetExtraContext<ModPorterContext>();

    /// <summary>
    /// Get a mod package from this universe given the package or resource key.
    /// </summary>
    public static ModPackage GetModPackage(this Universe universe, string packageOrResourceKey)
      => universe.GetExtraContext<ModPorterContext>().GetModPackage(packageOrResourceKey);

    /// <summary>
    /// Get a mod package from this universe given the package or resource key.
    /// </summary>
    public static IEnumerable<IPortableArchetype> TryToGetModResources(this Universe universe, string resourceKey)
      => universe.GetExtraContext<ModPorterContext>().TryToGetResourcesOfUnknownType(resourceKey);

    /// <summary>
    /// Get a mod package from this universe given the package or resource key.
    /// </summary>
    public static IEnumerable<IPortableArchetype> GetModResources<TArchetype>(this Universe universe, string resourceKey)
      where TArchetype : Archetype, IPortableArchetype
        => universe.GetExtraContext<ModPorterContext>().GetResources<TArchetype>(resourceKey);

  }
}

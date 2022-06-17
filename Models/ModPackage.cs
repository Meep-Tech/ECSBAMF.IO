using Meep.Tech.Collections.Generic;
using Meep.Tech.Data.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Meep.Tech.Data.IO {

  /// <summary>
  /// Contains metadata about a mod-package.
  /// </summary>
  public class ModPackage {
    internal static Dictionary<string, ModPackage> _modPackagesByWaitingAssemblies
      = new();

    /// <summary>
    /// The unique name of this mod package
    /// </summary>
    public string Key {
      get;
    }

    /// <summary>
    /// The full names of the assembly plugins imported via this mod.
    /// </summary>
    public IEnumerable<string> PluginAssemblyFiles
      => PluginAssemblyFiles; HashSet<string> _pluginAssemblies
        = new();

    /// <summary>
    /// The full list of resource keys for resources imported by this mod.
    /// </summary>
    public IEnumerable<string> ResourceKeys
      => _resourceKeys; HashSet<string> _resourceKeys
        = new();

    /// <summary>
    /// The archetypes imported by this mod from any pluign assemblies.
    /// </summary>
    public IEnumerable<Archetype> ImportedPluginBasedArchetypes
      => _importedPluginArchetypes; internal HashSet<Archetype> _importedPluginArchetypes
        = new();

    /// <summary>
    /// All imported archetypes in this mod based on a data resource with a key.
    /// </summary>
    public IEnumerable<IPortableArchetype> ImportedResourceBasedArchetypes
      => _importedArchetypesByResourceKey
        .Values
        .SelectMany(e => e.Values)
        .SelectMany(Comparitors.Identity);

    /// <summary>
    /// The archetypes imported by this mod package, indexed by the resource key used to import them.
    /// </summary>
    readonly Dictionary<System.Type, Dictionary<string, HashSet<IPortableArchetype>>> _importedArchetypesByResourceKey
      = new();

    internal ModPackage(string key) {
      Key = key;
    }

    #region Get

    /// <summary>
    /// Get the archetypes for the given resource key and type.
    /// </summary>
    public IEnumerable<IPortableArchetype> GetResourceBasedArchetype(Type assetArchetypeType, string resourceKey)
      => _importedArchetypesByResourceKey
      [assetArchetypeType.TryToGetAsArchetype().BaseArchetype]
      [resourceKey];

    /// <summary>
    /// Try to get the archetypes for the given resource key and type.
    /// Returns empty enumerable on failure to find.
    /// </summary>
    public IEnumerable<IPortableArchetype> TryToGetResourceBasedArchetype(Type assetArchetypeType, string resourceKey)
      => _importedArchetypesByResourceKey.TryGetValue(assetArchetypeType.TryToGetAsArchetype().BaseArchetype, out var foundTypeCollection)
      ? foundTypeCollection.TryGetValue(resourceKey, out var foundItems)
        ? foundItems
        : Enumerable.Empty<IPortableArchetype>()
      : Enumerable.Empty<IPortableArchetype>();

    /// <summary>
    /// Try to get the archetypes for the given resource key and type.
    /// provides an empty enumerable on failure to find.
    /// </summary>
    public bool TryToGetResourceBasedArchetype(Type assetArchetypeType, string resourceKey, out IEnumerable<IPortableArchetype> foundArchetypes)
      => (foundArchetypes = _importedArchetypesByResourceKey.TryGetValue(assetArchetypeType.TryToGetAsArchetype().BaseArchetype, out var foundTypeCollection)
      ? foundTypeCollection.TryGetValue(resourceKey, out var foundItems)
        ? foundItems
        : Enumerable.Empty<IPortableArchetype>()
      : Enumerable.Empty<IPortableArchetype>()).Any();

    /// <summary>
    /// Get the archetypes for the given resource key and type.
    /// </summary>
    public IEnumerable<TArchetype> GetResourceBasedArchetype<TArchetype>(string resourceKey)
      where TArchetype : Archetype, IPortableArchetype
       => GetResourceBasedArchetype(typeof(TArchetype), resourceKey)
        .Cast<TArchetype>();

    /// <summary>
    /// Try to get the archetypes for the given resource key and type.
    /// Returns empty enumerable on failure to find.
    /// </summary>
    public IEnumerable<TArchetype> TryToGetResourceBasedArchetype<TArchetype>(string resourceKey)
      where TArchetype : Archetype, IPortableArchetype
       => TryToGetResourceBasedArchetype(typeof(TArchetype), resourceKey)
        .Cast<TArchetype>();

    /// <summary>
    /// Try to get the archetypes for the given resource key and type.
    /// provides an empty enumerable on failure to find.
    /// </summary>
    public bool TryToGetResourceBasedArchetype<TArchetype>(string resourceKey, out IEnumerable<TArchetype> foundArchetypes)
      where TArchetype : Archetype, IPortableArchetype
       => (foundArchetypes = TryToGetResourceBasedArchetype(typeof(TArchetype), resourceKey, out var found)
        ? found.Cast<TArchetype>()
        : Enumerable.Empty<TArchetype>()).Any();

    /// <summary>
    /// Try to find any archetypes with the given resource key, reguardless of their base type.
    /// Returns empty if none found.
    /// </summary>
    public IEnumerable<IPortableArchetype> TryToGetResourceBasedArchetypeOfUnknownType(string resourceKey)
       => _importedArchetypesByResourceKey.SelectMany(v => v.Value)
        .Where(v => v.Key == resourceKey)
        .SelectMany(v => v.Value);

    /// <summary>
    /// Try to find any archetypes with the given resource key, reguardless of their base type.
    /// provides empty if none found.
    /// </summary>
    public bool TryToGetResourceBasedArchetypeOfUnknownType(string resourceKey, out IEnumerable<IPortableArchetype> foundArchetypes)
       => (foundArchetypes = _importedArchetypesByResourceKey.SelectMany(v => v.Value)
        .Where(v => v.Key == resourceKey)
        .SelectMany(v => v.Value)).Any();

    #endregion

    internal void _addModAsset(System.Type archetypeBaseType, string resourceKey, IEnumerable<IPortableArchetype> assetArchetypes) {
      if (_importedArchetypesByResourceKey.TryGetValue(archetypeBaseType, out var modItemsForArchetypeBaseType)) {
        assetArchetypes.ForEach(a => {
          if (modItemsForArchetypeBaseType.TryGetValue(resourceKey, out var existingItemsForResourceKey)) {
            if (!existingItemsForResourceKey.Add(a)) {
              throw new ArgumentException($"Archetype: {a} has already been added to the modpack: {Key}");
            }
          } else {
            modItemsForArchetypeBaseType[resourceKey] = new HashSet<IPortableArchetype> { a };
            _resourceKeys.Add(resourceKey);
          }
        });
      } else {
        _importedArchetypesByResourceKey.Add(archetypeBaseType, new() {
          {resourceKey, assetArchetypes.ToHashSet()}
        });
        _resourceKeys.Add(resourceKey);
      }
    }

    internal void _removeModAsset(IPortableArchetype assetArchetype) {
      if (_importedArchetypesByResourceKey.TryGetValue((assetArchetype as Archetype).BaseArchetype, out var valuesForThisArchetypeType)) {
        if (valuesForThisArchetypeType.TryGetValue(assetArchetype.ResourceKey, out var valuesForKey)) {
          if (valuesForKey.Contains(assetArchetype)) {
            valuesForThisArchetypeType.Remove(assetArchetype.ResourceKey);
            if (!valuesForKey.Any()) {
              _resourceKeys.Remove(assetArchetype.ResourceKey);
            }
          }
        }
      }
    }

		internal void _addAssemblyPlugin(string assemblyName) {
      _modPackagesByWaitingAssemblies.Add(assemblyName, this);
      _pluginAssemblies.Add(assemblyName);
		}
	}
}

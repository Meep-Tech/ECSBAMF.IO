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

    /// <summary>
    /// The unique name of this mod package
    /// </summary>
    public string Key {
      get;
    }

    /// <summary>
    /// All imported archetypes in this mod
    /// </summary>
    public IEnumerable<IPortableArchetype> ImportedArchetypes
      => _importedArchetypesByResourceKey
        .Values
        .SelectMany(e => e.Values)
        .SelectMany(Comparitors.Identity);

    /// <summary>
    /// The number of imported archetypes
    /// Uses selectmany, sum, and count.
    /// </summary>
    public int ImportedArchetypesCount
      => _importedArchetypesByResourceKey.Values.SelectMany(x => x.Values).Sum(x => x.Count());

    /// <summary>
    /// The archetypes imported by this mod package, indexed by the resource key used to import them.
    /// This value is cached and re-updated on the first call after a mod item is added or removed from this package.
    /// </summary>
    public IReadOnlyDictionary<System.Type, IReadOnlyDictionary<string, IEnumerable<IPortableArchetype>>> ImportedArchetypesByResourceKey 
      => __importedArchetypesByResourceKey ??= (IReadOnlyDictionary<System.Type, IReadOnlyDictionary<string, IEnumerable<IPortableArchetype>>>)_importedArchetypesByResourceKey
      .ToDictionary(e => e.Key, e => (IReadOnlyDictionary<string, IPortableArchetype>)e.Value.ToDictionary(
        e_e => e_e.Key,
        e_e => e_e.Value
      )); readonly Dictionary<System.Type, Dictionary<string, HashSet<IPortableArchetype>>> _importedArchetypesByResourceKey
        = new();
    IReadOnlyDictionary<System.Type, IReadOnlyDictionary<string, IEnumerable<IPortableArchetype>>> __importedArchetypesByResourceKey;

    internal ModPackage(string key) {
      Key = key;
    }

    /// <summary>
    /// Get the archetypes for the given resource key and type.
    /// </summary>
    public IEnumerable<IPortableArchetype> Get(Type assetArchetypeType, string resourceKey)
      => _importedArchetypesByResourceKey
      [assetArchetypeType.TryToGetAsArchetype().BaseArchetype]
      [resourceKey];

    /// <summary>
    /// Try to get the archetypes for the given resource key and type.
    /// Returns empty enumerable on failure to find.
    /// </summary>
    public IEnumerable<IPortableArchetype> TryToGet(Type assetArchetypeType, string resourceKey)
      => _importedArchetypesByResourceKey.TryGetValue(assetArchetypeType.TryToGetAsArchetype().BaseArchetype, out var foundTypeCollection)
      ? foundTypeCollection.TryGetValue(resourceKey, out var foundItems)
        ? foundItems
        : Enumerable.Empty<IPortableArchetype>()
      : Enumerable.Empty<IPortableArchetype>();

    /// <summary>
    /// Try to get the archetypes for the given resource key and type.
    /// provides an empty enumerable on failure to find.
    /// </summary>
    public bool TryToGet(Type assetArchetypeType, string resourceKey, out IEnumerable<IPortableArchetype> foundArchetypes)
      => (foundArchetypes = _importedArchetypesByResourceKey.TryGetValue(assetArchetypeType.TryToGetAsArchetype().BaseArchetype, out var foundTypeCollection)
      ? foundTypeCollection.TryGetValue(resourceKey, out var foundItems)
        ? foundItems
        : Enumerable.Empty<IPortableArchetype>()
      : Enumerable.Empty<IPortableArchetype>()).Any();

    /// <summary>
    /// Get the archetypes for the given resource key and type.
    /// </summary>
    public IEnumerable<TArchetype> Get<TArchetype>(string resourceKey)
      where TArchetype : Archetype, IPortableArchetype
       => Get(typeof(TArchetype), resourceKey)
        .Cast<TArchetype>();

    /// <summary>
    /// Try to get the archetypes for the given resource key and type.
    /// Returns empty enumerable on failure to find.
    /// </summary>
    public IEnumerable<TArchetype> TryToGet<TArchetype>(string resourceKey)
      where TArchetype : Archetype, IPortableArchetype
       => TryToGet(typeof(TArchetype), resourceKey)
        .Cast<TArchetype>();

    /// <summary>
    /// Try to get the archetypes for the given resource key and type.
    /// provides an empty enumerable on failure to find.
    /// </summary>
    public bool TryToGet<TArchetype>(string resourceKey, out IEnumerable<TArchetype> foundArchetypes)
      where TArchetype : Archetype, IPortableArchetype
       => (foundArchetypes = TryToGet(typeof(TArchetype), resourceKey, out var found)
        ? found.Cast<TArchetype>()
        : Enumerable.Empty<TArchetype>()).Any();

    /// <summary>
    /// Try to find any archetypes with the given resource key, reguardless of their base type.
    /// Returns empty if none found.
    /// </summary>
    public IEnumerable<IPortableArchetype> TryToGetResourcesOfUnknownType(string resourceKey)
       => _importedArchetypesByResourceKey.SelectMany(v => v.Value)
        .Where(v => v.Key == resourceKey)
        .SelectMany(v => v.Value);

    /// <summary>
    /// Try to find any archetypes with the given resource key, reguardless of their base type.
    /// provides empty if none found.
    /// </summary>
    public bool TryToGetResourcesOfUnknownType(string resourceKey, out IEnumerable<IPortableArchetype> foundArchetypes)
       => (foundArchetypes = _importedArchetypesByResourceKey.SelectMany(v => v.Value)
        .Where(v => v.Key == resourceKey)
        .SelectMany(v => v.Value)).Any();

    internal void _addModAsset(System.Type archetypeBaseType, string resourceKey, IEnumerable<IPortableArchetype> assetArchetypes) {
      if (_importedArchetypesByResourceKey.TryGetValue(archetypeBaseType, out var modItemsForArchetypeBaseType)) {
        assetArchetypes.ForEach(a => {
          if (modItemsForArchetypeBaseType.TryGetValue(resourceKey, out var existingItemsForResourceKey)) {
            if (!existingItemsForResourceKey.Add(a)) {
              throw new ArgumentException($"Archetype: {a} has already been added to the modpack: {Key}");
            }
          } else {
            modItemsForArchetypeBaseType[resourceKey] = new HashSet<IPortableArchetype> { a };
          }
        });
      } else {
        _importedArchetypesByResourceKey.Add(archetypeBaseType, new() {
          {resourceKey, assetArchetypes.ToHashSet()}
        });
      }

      // clear cache
      __importedArchetypesByResourceKey = null;
    }

    internal void _removeModAsset(IPortableArchetype assetArchetype) {
      if (_importedArchetypesByResourceKey.TryGetValue((assetArchetype as Archetype).BaseArchetype, out var valuesForThisArchetypeType)) {
        if (valuesForThisArchetypeType.TryGetValue(assetArchetype.ResourceKey, out var valuesForKey)) {
          if (valuesForKey.Contains(assetArchetype)) {
            valuesForThisArchetypeType.Remove(assetArchetype.ResourceKey);

            // clear cache
            __importedArchetypesByResourceKey = null;
          }
        }
      }
    }
  }
}

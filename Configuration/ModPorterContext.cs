using Meep.Tech.Collections.Generic;
using Meep.Tech.Data.Reflection;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters;

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
    /// The root mod folder, where mods are loaded from.
    /// </summary>
    public string RootModsFolder {
      get;
    }

    /// <summary>
    /// The root data folder, where in game models are saved to
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
      universe.Loader.Options.DataFolderParentFolderLocation = rootApplicationPersistentDataFolder;
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
    protected override Action OnLoaderFinalizeStart
      => () => _portersByModelSubFolder = _modelPorters.Values.ToDictionary(p => p.GetSaveToRootFolder());

    ///<summary><inheritdoc/></summary>
    protected override Action<Archetype> OnUnloadArchetype => archetype => {
      // remove from mod assets.
      if (archetype is IPortableArchetype portableType) {
        GetModPackage(portableType.PackageKey)
          ._removeModAsset(portableType);
      }
    };

    /// <summary>
    /// Grab auto included porters from the provided interface.
    /// </summary>
    protected override Action<bool, Type, Archetype, Exception, bool> OnLoaderArchetypeInitializationComplete
      => (success, archetypeType, archetype, error, isSplayed) => {
        if (success) {
          if (ModPackage._modPackagesByWaitingAssemblies.TryGetValue(archetypeType.Assembly.Location, out ModPackage package)) {
            // TODO: here should maybe be where we try to import the assets for plugin archetypes.
            package._importedPluginArchetypes.Add(archetype);
          }

          foreach (System.Type iHavePorterInterface in archetypeType.GetAllInheritedGenericTypes(typeof(IHavePortableModel<>))) {
            var forModelType = iHavePorterInterface.GetGenericArguments().First();
            if (!_modelPorters.ContainsKey(forModelType)) {
              _modelPorters[forModelType] = (ModelPorter)iHavePorterInterface
                .GetMethod("CreateModelPorter", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .Invoke(archetype, new object[0]);
            }
          }
        }
      };

    /// <summary>
    /// Add in the special converters logic
    /// </summary>
    protected override Action<MemberInfo, JsonProperty> OnLoaderModelJsonPropertyCreationComplete
      => (MemberInfo memberInfo, JsonProperty defaultJsonProperty) => {
        AutoPortAttribute attribute;
        if ((attribute = memberInfo.GetCustomAttribute<AutoPortAttribute>()) != null) {
          var itemType = memberInfo is FieldInfo f ? f.FieldType : memberInfo is PropertyInfo p ? p.PropertyType : null;
          defaultJsonProperty.Converter = typeof(IUnique).IsAssignableFrom(itemType)
            ? new Meep.Tech.Data.IO.Configuration.PortableModelJsonConverter(Universe)
            : (itemType.IsAssignableToGeneric(typeof(IReadOnlyDictionary<,>)) 
              && typeof(IUnique).IsAssignableFrom(itemType.GetGenericArguments().Last())
              && (typeof(string) == itemType.GetGenericArguments().First())
            ) ? attribute.PreserveKeys
                ? (Newtonsoft.Json.JsonConverter)Activator.CreateInstance(
                    typeof(Configuration.PortableModelsDictionaryWithKeysJsonConverter<>).MakeGenericType(itemType.GetGenericArguments().Last()),
                    Universe)
                : (Newtonsoft.Json.JsonConverter)Activator.CreateInstance(
                    typeof(Configuration.PortableModelsDictionaryJsonConverter<>).MakeGenericType(itemType.GetGenericArguments().Last()),
                    Universe)
              : itemType.IsAssignableToGeneric(typeof(IEnumerable<>)) && typeof(IUnique).IsAssignableFrom(itemType.GetGenericArguments().First())
                ? (Newtonsoft.Json.JsonConverter)Activator.CreateInstance(
                  typeof(Configuration.PortableModelsCollectionJsonConverter<>).MakeGenericType(itemType.GetGenericArguments().First()),
                  Universe)
                : throw new InvalidOperationException($"{nameof(AutoPortAttribute)} only works on properties that inherit from IUnique, or IDictionary<string,IUnique>, or IEnumerable<IUnique>. {itemType.FullName} is an invalid type.");

          defaultJsonProperty.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
        }
      };

    static MethodInfo _stringGetter
       = typeof(BuilderExtensions).GetMethod(nameof(BuilderExtensions.TryToGetParam))
        .MakeGenericMethod(typeof(string));
    static MethodInfo _stringEnumerableGetter
       = typeof(BuilderExtensions).GetMethod(nameof(BuilderExtensions.TryToGetParam))
        .MakeGenericMethod(typeof(IEnumerable<object>));
    static MethodInfo _stringDictionaryGetter
       = typeof(BuilderExtensions).GetMethod(nameof(BuilderExtensions.TryToGetParam))
        .MakeGenericMethod(typeof(Dictionary<string, object>));
    static MethodInfo _stringEnumerableBackupGetter
       = typeof(BuilderExtensions).GetMethod(nameof(BuilderExtensions.TryToGetParam))
        .MakeGenericMethod(typeof(IEnumerable<string>));
    static MethodInfo _stringDictionaryBackupGetter
       = typeof(BuilderExtensions).GetMethod(nameof(BuilderExtensions.TryToGetParam))
        .MakeGenericMethod(typeof(Dictionary<string, string>));

    /// <summary>
    /// Adds auto porting steps for auto building.
    /// </summary>
    protected override Action<Type, AutoBuildAttribute, PropertyInfo> OnLoaderAutoBuildPropertyCreationStart
      => (modelType, autoBuildData, fieldInfo) => {
        AutoPortAttribute autoPortAttribute;
        if ((autoPortAttribute = fieldInfo.GetCustomAttribute<AutoPortAttribute>(true)) != null) {

          // check for skipping
          if (autoPortAttribute.IgnoreDuringAutoBuilding) {
            return;
          }

          // if not, these types get a custom getter
          autoBuildData.Getter
            = new((m,b,p,a,r) => {
              MethodInfo getterFunc = p.PropertyType.IsAssignableToGeneric(typeof(IReadOnlyDictionary<,>))
                ? _stringDictionaryGetter
                : p.PropertyType.IsAssignableToGeneric(typeof(IEnumerable<>)) 
                  ? _stringEnumerableGetter
                  : _stringGetter;

              return AutoBuildAttribute.BuildDefaultGetterFromBuilderOrDefault(m, b, p, a, (m, b, p, a, r) => {
                object[] parameters = new object[] { b, a.ParameterName ?? p.Name, null, default };

                if ((bool)getterFunc.Invoke(null, parameters)) {
                  // if we succeeded in our get and it's a dictionary type field:
                  if (getterFunc == _stringDictionaryGetter) {
                    // if we need to preserve the keys
                    if (autoPortAttribute.PreserveKeys) {
                      return _getDictionaryWithPreservedKeysFromDictionary(fieldInfo, parameters);
                    } // if the keys are just the item ids
                    else {
                      return _getDictionaryFromDictionary(fieldInfo, parameters);
                    }
                  } // if ifs for an enumerable type
                  else if (getterFunc == _stringEnumerableGetter) {
                    return _getForEnumerableFromEnumerable(fieldInfo, parameters);
                  } // if we succeeded for a regular single model
                  else {
                    return parameters[2] is string id
                        ? GetModelPorter(p.PropertyType).LoadByKey(id)
                        : parameters[2];
                  }
                } // if we failed and it's a dictionary getter, try the backup
                else if (getterFunc == _stringDictionaryGetter) {
                  // if the backup succeeds
                  if ((bool)_stringDictionaryBackupGetter.Invoke(null, parameters)) {
                    // if we need to preserve the keys
                    if (autoPortAttribute.PreserveKeys) {
                      return _getDictionaryWithPreservedKeysFromDictionaryBackup(fieldInfo, parameters);
                    } // if the keys are just the item ids
                    else {
                      return _getDictionaryFromDictionaryBackup(fieldInfo, parameters);
                    }
                  }

                  // if we failed the backup but have preserve keys off, we can try to get and adapt an enumerable.
                  if (!autoPortAttribute.PreserveKeys) {
                    // backup to use IE<object> in place of ID<string, object>
                    if (!(bool)_stringEnumerableGetter.Invoke(null, parameters)) {
                      // backup for IE<string>
                      if ((bool)_stringEnumerableBackupGetter.Invoke(null, parameters)) {
                        return _getDictionaryFromEnumerableBackup(fieldInfo, parameters);
                      }
                    }
                    else {
                      return _getDictionaryFromEnumerable(fieldInfo, parameters);
                    }
                  }
                }
                else if (getterFunc == _stringEnumerableGetter) {
                  if ((bool)_stringEnumerableBackupGetter.Invoke(null, parameters)) {
                    return _getFromEnumerableBackup(fieldInfo, parameters);
                  }
                }

                return !r
                  ? AutoBuildAttribute.GetDefaultValue(m, b, p, a, r)
                  : throw new IModel.Builder.Param.MissingException($"Missing param of type {p.PropertyType} or portable id for that type as a param with the name:{a.ParameterName ?? p.Name}");
              });
            });
        }
      };

    object _getDictionaryFromDictionaryBackup(PropertyInfo fieldInfo, object[] parameters) {
      Type modelToPortType = fieldInfo.DeclaringType.GenericTypeArguments[1];
      IDictionary result = Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string), modelToPortType)) as IDictionary;
      foreach (var entry in parameters[2] as Dictionary<string, string>) {
        object value = GetModelPorter(modelToPortType).LoadByKey(entry.Value);
        result.Add((value as IUnique).Id, value);
      }

      return result;
    }

    object _getDictionaryWithPreservedKeysFromDictionaryBackup(PropertyInfo fieldInfo, object[] parameters) {
      Type modelToPortType = fieldInfo.DeclaringType.GenericTypeArguments[1];
      IDictionary result = Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string), modelToPortType)) as IDictionary;
      foreach (var entry in parameters[2] as Dictionary<string, string>) {
        object value = GetModelPorter(modelToPortType).LoadByKey(entry.Value);
        result.Add(entry.Key, value);
      }

      return result;
    }

    object _getDictionaryFromDictionary(PropertyInfo fieldInfo, object[] parameters) {
      Type modelToPortType = fieldInfo.DeclaringType.GenericTypeArguments[1];
      IDictionary result = Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string), modelToPortType)) as IDictionary;
      foreach (var entry in parameters[2] as Dictionary<string, object>) {
        object value = entry.Value is string
          ? GetModelPorter(modelToPortType).LoadByKey(entry.Value as string)
          : entry.Value;
        result.Add((value as IUnique).Id, value);
      }

      return result;
    }

    object _getDictionaryWithPreservedKeysFromDictionary(PropertyInfo fieldInfo, object[] parameters) {
      Type modelToPortType = fieldInfo.DeclaringType.GenericTypeArguments[1];
      IDictionary result = Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string), modelToPortType)) as IDictionary;
      foreach (var entry in parameters[2] as Dictionary<string, object>) {
        object value = entry.Value is string
          ? GetModelPorter(modelToPortType).LoadByKey(entry.Value as string)
          : entry.Value;
        result.Add(entry.Key, value);
      }

      return result;
    }

    object _getDictionaryFromEnumerableBackup(PropertyInfo fieldInfo, object[] parameters) {
      Type modelToPortType = fieldInfo.DeclaringType.GenericTypeArguments[0];
      IDictionary result = Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string), modelToPortType)) as IDictionary;
      foreach (var entry in parameters[2] as IEnumerable<string>) {
        object value = GetModelPorter(modelToPortType).LoadByKey(entry);
        result.Add((value as IUnique).Id, value);
      }

      return result;
    }

    object _getDictionaryFromEnumerable(PropertyInfo fieldInfo, object[] parameters) {
      Type modelToPortType = fieldInfo.DeclaringType.GenericTypeArguments[0];
      IDictionary result = Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string), modelToPortType)) as IDictionary;
      foreach (var entry in parameters[2] as IEnumerable<object>) {
        object value = entry is string
          ? GetModelPorter(modelToPortType).LoadByKey(entry as string)
          : entry;
        result.Add((entry as IUnique).Id, value);
      }

      return result;
    }

    object _getFromEnumerableBackup(PropertyInfo fieldInfo, object[] parameters) {
      Type modelToPortType = fieldInfo.DeclaringType.GenericTypeArguments[0];
      IList result = Activator.CreateInstance(typeof(List<>).MakeGenericType(modelToPortType)) as IList;
      foreach (var entry in parameters[2] as IEnumerable<string>) {
        object value = GetModelPorter(modelToPortType).LoadByKey(entry);
        result.Add(value);
      }

      return result;
    }

    object _getForEnumerableFromEnumerable(PropertyInfo fieldInfo, object[] parameters) {
      Type modelToPortType = fieldInfo.DeclaringType.GenericTypeArguments[0];
      IList result = Activator.CreateInstance(typeof(List<>).MakeGenericType(modelToPortType)) as IList;
      foreach (var entry in parameters[2] as IEnumerable<object>) {
        object value = entry is string
          ? GetModelPorter(modelToPortType).LoadByKey(entry as string)
          : entry;
        result.Add(value);
      }

      return result;
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
      where TModel : class, IModel
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
      where TModel : class, IModel
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
    public ModelPorter GetModelPorter(System.Type modelType)
      => _modelPorters.TryGetValue(modelType, out var found)
        ? found
        : (_modelPorters[modelType] = _modelPorters
          .OrderByDescending(porter => porter.Key.GetDepthOfInheritance())
          .First(porter => porter.Value.ModelBaseType.IsAssignableFrom(modelType)).Value);

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
      where TModel : class, IModel {
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
    public IEnumerable<IPortableArchetype> GetResourceBasedArchetypes(Type assetArchetypeType, string resourceKey)
      => GetModPackage(resourceKey).GetResourceBasedArchetype(assetArchetypeType, resourceKey);

    /// <summary>
    /// Try to get the archetypes for the given resource key and type.
    /// Returns empty enumerable on failure to find.
    /// </summary>
    public IEnumerable<IPortableArchetype> TryToGetResourceBasedArchetypes(Type assetArchetypeType, string resourceKey)
      => GetModPackage(resourceKey).TryToGetResourceBasedArchetype(assetArchetypeType, resourceKey);

    /// <summary>
    /// Try to get the archetypes for the given resource key and type.
    /// provides an empty enumerable on failure to find.
    /// </summary>
    public bool TryToGetResourceBasedArchetypes(Type assetArchetypeType, string resourceKey, out IEnumerable<IPortableArchetype> foundArchetypes)
      => GetModPackage(resourceKey).TryToGetResourceBasedArchetype(assetArchetypeType, resourceKey, out foundArchetypes);

    /// <summary>
    /// Get the archetypes for the given resource key and type.
    /// </summary>
    public IEnumerable<TArchetype> GetResourceBasedArchetypes<TArchetype>(string resourceKey)
      where TArchetype : Archetype, IPortableArchetype
        => GetModPackage(resourceKey).GetResourceBasedArchetype<TArchetype>(resourceKey);

    /// <summary>
    /// Try to get the archetypes for the given resource key and type.
    /// Returns empty enumerable on failure to find.
    /// </summary>
    public IEnumerable<TArchetype> TryToGetResourceBasedArchetypes<TArchetype>(string resourceKey)
      where TArchetype : Archetype, IPortableArchetype
        => GetModPackage(resourceKey).TryToGetResourceBasedArchetype<TArchetype>(resourceKey);

    /// <summary>
    /// Try to get the archetypes for the given resource key and type.
    /// provides an empty enumerable on failure to find.
    /// </summary>
    public bool TryToGetResourceBasedArchetypes<TArchetype>(string resourceKey, out IEnumerable<TArchetype> foundArchetypes)
      where TArchetype : Archetype, IPortableArchetype
        => GetModPackage(resourceKey).TryToGetResourceBasedArchetype(resourceKey, out foundArchetypes);

    /// <summary>
    /// Try to find any archetypes with the given resource key, reguardless of their base type.
    /// Returns empty if none found.
    /// </summary>
    public IEnumerable<IPortableArchetype> TryToGetResourceBasedArchetypesOfUnknownType(string resourceKey)
        => GetModPackage(resourceKey).TryToGetResourceBasedArchetypeOfUnknownType(resourceKey);

    /// <summary>
    /// Try to find any archetypes with the given resource key, reguardless of their base type.
    /// provides empty if none found.
    /// </summary>
    public bool TryToGetResourceBasedArchetypesOfUnknownType(string resourceKey, out IEnumerable<IPortableArchetype> foundArchetypes)
        => GetModPackage(resourceKey).TryToGetResourceBasedArchetypeOfUnknownType(resourceKey, out foundArchetypes);

    #endregion

    internal void _updateModPackageFromPlugin(
      string packageKey,
      string assemblyName
    ) {
      if (!_importedMods.TryGetValue(packageKey, out var existingMod)) {
        existingMod = _importedMods[packageKey] = new ModPackage(packageKey);
      }
      existingMod._addAssemblyPlugin(assemblyName);
    }

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
  }
}

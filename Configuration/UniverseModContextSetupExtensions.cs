using System.Collections.Generic;
using System.IO;
using System.Linq;
using Meep.Tech.Collections.Generic;
using Meep.Tech.Data.Configuration;

namespace Meep.Tech.Data.IO {
  public static class UniverseModContextSetupExtensions {

    /// <summary>
    /// Add default mod import settings using just the porters to use.
    /// </summary>
    /// <param name="rootApplicationPersistentDataFolder">The directory to put the mods folder inside of</param>
    public static void AddModImportContext(this Universe universe, string rootApplicationPersistentDataFolder, IEnumerable<ArchetypePorter> archetypePorters = null, IEnumerable<ModelPorter> modelPorters = null) {
      universe.SetExtraContext(new ModPorterContext(universe, rootApplicationPersistentDataFolder, archetypePorters, modelPorters ?? Enumerable.Empty<ModelPorter>()));
    }

    /// <summary>
    /// Add a custom mod import settings object of your own.
    /// </summary>
    public static void AddModImportContext(this Universe universe, ModPorterContext modPorterSettings) {
      universe.SetExtraContext(modPorterSettings);
    }

    /// <summary>
    /// Set mod assemblies to load in order by a list of mods.
    /// </summary>
    public static void AddModPluginAssemblies(this Loader loader, IEnumerable<string> modPackageKeys, int indexOffset = 1) {
      int modIndex = indexOffset;
      modPackageKeys.Select(k => Path.Combine(loader.Universe.GetModData().RootModsFolder, k, ArchetypePorter.PluginsSubFolderName))
        .SelectMany(pluginsFolder => Directory.GetFiles(pluginsFolder, "*.dll")
        .Where(pluginFileName => {
          string trimmedFileName = Path.GetFileName(pluginFileName);
          return !trimmedFileName.StartsWith(".")
            && !trimmedFileName.StartsWith("_");
        })
      ).ForEach(modAssemblyFile => {
        loader.Options.PreOrderedAssemblyFiles.Add((ushort)(modIndex++), modAssemblyFile);
      });
    }
  }
}

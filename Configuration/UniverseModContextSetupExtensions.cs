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
      universe.SetExtraContext(new ModPorterContext(universe, rootApplicationPersistentDataFolder, archetypePorters, modelPorters));
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
    public static void AddModPluginAssemblies(this Universe universe, IEnumerable<string> modPackageKeys, int indexOffset = 1) {
      int modIndex = indexOffset;
      modPackageKeys.Select(k => Path.Combine(universe.GetModData().RootModsFolder, k, ArchetypePorter.PluginsSubFolderName))
        .SelectMany(pluginsFolder => Directory.GetFiles(pluginsFolder, "*.dll")
        .Where(pluginFileName => {
          string trimmedFileName = Path.GetFileName(pluginFileName);
          return !trimmedFileName.StartsWith(".")
            && !trimmedFileName.StartsWith("_");
        })
      ).ForEach(modAssemblyFile => {
        universe.Loader.Options.PreOrderedAssemblyFiles.Add((ushort)(modIndex++), modAssemblyFile);
      });
    }

    /// <summary>
    /// Loads all pluigns for all mods in the default mods folder in order discovered.
    /// </summary>
    public static void AddAllModPluginAssemblies(this Universe universe, int indexOffset = 1) {
      int modIndex = indexOffset;
      foreach (string modFolderLocation in Directory.GetDirectories(universe.GetModData().RootModsFolder)) {
				string pluginsPath = Path.Combine(modFolderLocation, ArchetypePorter.PluginsSubFolderName);
        if (Directory.Exists(pluginsPath)) {
					List<string> assemblies = Directory.GetFiles(
            pluginsPath, 
            "*.dll", 
            SearchOption.AllDirectories
          ).OrderBy(f => f.Replace(".", "")).ToList();
          foreach (string pluginFileLocation in assemblies) {
            if (Path.GetFileName(pluginFileLocation) is string f && !f.StartsWith(".") && (!f.StartsWith("_"))) {
              universe.GetModData()._updateModPackageFromPlugin(
                Path.GetDirectoryName(modFolderLocation),
                pluginFileLocation
              );
              universe.Loader.Options.PreOrderedAssemblyFiles.Add((ushort)(modIndex++), pluginFileLocation);
            }
          }
        }
      }
    }
  }
}

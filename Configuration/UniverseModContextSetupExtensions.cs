using System.Collections.Generic;

namespace Meep.Tech.Data.IO {
  public static class UniverseModContextSetupExtensions {

    /// <summary>
    /// Add default mod import settings using just the porters to use.
    /// </summary>
    /// <param name="rootApplicationPersistentDataFolder">The directory to put the mods folder inside of</param>
    public static void AddModImportContext(this Universe universe, string rootApplicationPersistentDataFolder, IEnumerable<ArchetypePorter> porters) {
      universe.SetExtraContext(new ModPorterContext(universe, rootApplicationPersistentDataFolder, porters));
    }

    /// <summary>
    /// Add a custom mod import settings object of your own.
    /// </summary>
    public static void AddModImportContext(this Universe universe, ModPorterContext modPorterSettings) {
      universe.SetExtraContext(modPorterSettings);
    }
  }
}

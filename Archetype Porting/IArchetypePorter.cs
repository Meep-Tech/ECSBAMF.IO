using Meep.Tech.Data;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Meep.Tech.Data.IO {

  /// <summary>
  /// used to im/export archetypes from mods
  /// </summary>
  public interface IArchetypePorter {

    /// <summary>
    /// Get an already loaded archetype
    /// </summary>
    Archetype GetCachedArchetype(string resourceKey);

    /// <summary>
    /// Try to get an already loaded archetype
    /// </summary>
    Archetype TryToGetGetCachedArchetype(string resourceKey);

    /// <summary>
    /// get an archetype from the mods folder files
    /// </summary>
    Archetype LoadArchetypeFromModFolder(string resourceKey, Dictionary<string, object> options = null);

    /// <summary>
    /// Try to get an existing archetype from the compiled mod folder files.
    /// This doesn't throw if it finds no files, but may throw if the found files are invalid, or the archetype already exists.
    /// Returns null on failure to find.
    /// </summary>
    Archetype TryToFindArchetypeAndLoadFromModFolder(string resourceKey, Dictionary<string, object> options = null);

    /// <summary>
    /// Import and build all archetypes from the provided loose files and folder names.
    /// </summary>
    IEnumerable<Archetype> ImportAndBuildNewArchetypesFromLooseFilesAndFolders(string[] externalFileAndFolderLocations, Dictionary<string, object> options, out HashSet<string> processedFiles);

    /// <summary>
    /// Import and build all archetypes from the provided mods folder location using the expected mods folder structure.
    /// </summary>
    IEnumerable<Archetype> ImportAndBuildNewArchetypesFromModsFolder(Dictionary<string, object> options);

    /// <summary>
    /// Import and build all archetypes from the provided imports folder location using the expected mods folder structure.
    /// </summary>
    IEnumerable<Archetype> ImportAndPackageModsFromImportsFolder(Dictionary<string, object> options);

    /// <summary>
    /// Construct all of the needed keys for an asset/archetype.
    /// </summary>
    /// <param name="primaryAssetFilename">The promary asset file being imported</param>
    /// <param name="fromSingleArchetypeFolder">If this is from a single archetype folder (not loose assets)</param>
    (string resourceName, string packageName, string resourceKey) ConstructArchetypeKeys(string primaryAssetFilename, Dictionary<string, object> options, JObject config);

    /// <summary>
    /// Get the sub folder under the mod folder on the device used for this specfic archetype,
    /// also splits up the key into it's parts
    /// </summary>
    string GetArchetypeFolderAndDeconstructKey(string resouceKey, out string resourceName, out string packageName);

    /// <summary>
    /// Get an archetype folder from just the resource key.
    /// </summary>
    string GetArchetypeFolderFromKey(string resourceKey);

    /// <summary>
    /// Get the folder for a given archetype
    /// </summary>
    string GetFolderFor(Archetype portableArchetype);

    /// <summary>
    /// Get the current default package name
    /// </summary>
    string GetDefaultPackageName();

    /// <summary>
    /// Serialize this archetype to a set of files in the mod folder.
    /// </summary>
    /// <param name="archetype">The archetype to serialize into a file or files</param>
    /// <returns>The newly serialized file's locations</returns>
    public string[] SerializeArchetypeToModFolder(Archetype archetype);
  }
}
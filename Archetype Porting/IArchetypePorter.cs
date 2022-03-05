using Meep.Tech.Data;
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
    IEnumerable<Archetype> ImportAndBuildNewArchetypesFromLooseFilesAndFolders(string[] externalFileAndFolderLocations, Dictionary<string, object> options, out IEnumerable<string> processedFiles);

    /// <summary>
    /// Import and build all archetypes from the provided mods folder location using the expected mods folder structure.
    /// </summary>
    IEnumerable<Archetype> ImportAndBuildNewArchetypesFromModsFolder(Dictionary<string, object> options);

    /// <summary>
    /// Import and build all archetypes from the provided imports folder location using the expected mods folder structure.
    /// </summary>
    IEnumerable<Archetype> ImportAndPackageModsFromImportsFolder(Dictionary<string, object> options);

    /// <summary>
    /// Get the sub folder under the mod folder on the device used for this specfic archetype,
    /// also splits up the key into it's parts
    /// </summary>
    string GetFolderForModItem(string resouceKey, out string resourceName, out string packageName);    

    /// <summary>
    /// Get the sub folder unther the mod folder on the device used for this specfic archetype
    /// </summary>
    string GetFolderForModItem(string resourceName, string packageName);

    /// <summary>
    /// Get the sub folder under the mod folder on the device used for this specfic archetype
    /// </summary>
    string GetFolderForArchetype(IPortableArchetype archetype);

    /// <summary>
    /// Serialize this archetype to a set of files in the mod folder.
    /// </summary>
    /// <param name="archetype">The archetype to serialize into a file or files</param>
    /// <returns>The newly serialized file's locations</returns>
    public string[] SerializeArchetypeToModFolder(Archetype archetype);

    /// <summary>
    /// Move an archetype from it's old name to a new folder with it's new name (within the same package)
    /// WARNING This overwrites any existing archetypes with the same name. Use try if you don't want to do this.
    /// </summary>
    void ForceMoveRenamedArchetypeFolder(string oldName, IPortableArchetype archetype);

    /// <summary>
    /// Move an archetype from it's old name to a new folder with it's new name (within the same package)
    /// This returns false if the file exists already, meaning there's already an archetype with the given key.
    /// </summary>
    bool TryToMoveRenamedArchetypeFolder(string oldName, IPortableArchetype archetype);
  }
}
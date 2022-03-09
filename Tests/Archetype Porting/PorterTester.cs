using Meep.Tech.Collections.Generic;
using Meep.Tech.Data.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Meep.Tech.Data.IO.Tests {

  /// <summary>
  /// A test of a porter and it's abilities.
  /// These tests create and then destroy a default universe, multi-threading tests is not currently supported.
  /// </summary>
  public partial class PorterTester<TArchetype> where TArchetype : Meep.Tech.Data.Archetype, IPortableArchetype {
    readonly string _folderContainingModsFolder;
    readonly Action<Loader.Settings> _xbamLoaderSettingsConfiguation;
    readonly IEnumerable<ArchetypePorter> _otherPorters;
    readonly HashSet<string> _dummyFiles;
    Universe _testUniverse;

    /// <summary>
    /// The porter to use in testing.
    /// </summary>
    public ArchetypePorter<TArchetype> Porter {
      get;
    }

    /// <summary>
    /// The mods folder to use for testing imports
    /// </summary>
    public string TestModsFolder {
      get => _testModsFolder ?? Path.Combine(_testUniverse.GetModData().RootModsFolder, Porter.GetDefaultPackageName(), Porter.SubFolderName, "___test");
      init => _testModsFolder = value;
    } string _testModsFolder;

    /// <summary>
    /// Make a new import tester.
    /// </summary>
    public PorterTester(
      string folderContainingModsFolder,
      ArchetypePorter<TArchetype> porterToTest,
      HashSet<string> dummyFileLocations = null,
      Action<Loader.Settings> xbamLoaderSettingsConfiguation = null,
      IEnumerable<ArchetypePorter> otherRequiredAuxilaryPorters = null
    ) {
      _folderContainingModsFolder = folderContainingModsFolder;
      Porter = porterToTest;
      _dummyFiles = dummyFileLocations;
      _xbamLoaderSettingsConfiguation = xbamLoaderSettingsConfiguation;
      _otherPorters = otherRequiredAuxilaryPorters;
    }

    /// <summary>
    /// Can be used by tests to get dummy files by name.
    /// </summary>
    protected bool TryToGetDummyFile(string fileName, out string dummyFileFullSystemLocation) {
      string foundFile;
      if ((foundFile =_dummyFiles.FirstOrDefault(f => Path.GetFileName(f) == fileName)) != null) {
        dummyFileFullSystemLocation = foundFile;
        return true;
      }

      dummyFileFullSystemLocation = null;
      return false;
    }

    /// <summary>
    /// Run all the provided tests using this porter and it's universe setup.
    /// Don't call this in a runtime with an existing default universe!
    /// </summary>
    public Dictionary<string, Test.TestResult> Run(IEnumerable<Test> tests) {
      _initXBam();

      // clear out the previous run's test mods folder.
      if (Directory.Exists(TestModsFolder)) {
        Directory.Delete(TestModsFolder, true);
      }
      Directory.CreateDirectory(TestModsFolder);

      Dictionary<string, Test.TestResult> results 
        = tests
          .ToDictionary(
            t => t.UniqueTestName, 
            t => t.RunOn(this)
          );
      _destroyXbam();

      return results;
    }

    protected virtual void _initXBam() {
      // Configure Settings
      Loader.Settings settings = new() {
        FatalOnCannotInitializeType = true
      };
      _xbamLoaderSettingsConfiguation?.Invoke(settings);

      /// Load Archetypes
      // it loader and universe
      Loader loader = new(settings);
      _testUniverse = new Universe(loader, $"test_porter{Porter.SubFolderName}");

      // add porter settings
      _testUniverse.AddModImportContext(
        _folderContainingModsFolder,
        Porter.AsSingleItemEnumerable()
          .Concat(_otherPorters ?? Enumerable.Empty<ArchetypePorter>())
      );

      // run loader
      loader.Initialize(_testUniverse);
    }

    protected virtual void _destroyXbam() {
      Archetypes.DefaultUniverse = null;
      Models.DefaultUniverse = null;
      Components.DefaultUniverse = null;
      Porter._universe = null;

      _testUniverse = null;
    }
  }
}

using Meep.Tech.Data;
using Meep.Tech.Data.Configuration;
using System;
using System.IO;

namespace Meep.Tech.Data.IO.Tests {

  /// <summary>
  /// A test of a porter and it's abilities.
  /// These tests create and then destroy a default universe, multi-threading tests is not currently supported.
  /// </summary>
  public partial class PorterTester<TArchetype> where TArchetype : Meep.Tech.Data.Archetype, IPortableArchetype {

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
      get => _testModsFolder ?? Path.Combine(ArchetypePorter.RootModsFolder, Porter.DefaultPackageName, "_test");
      init => _testModsFolder = value;
    }
    string _testModsFolder;

    /// <summary>
    /// Make a new import tester.
    /// </summary>
    public PorterTester(ArchetypePorter<TArchetype> porter, Action<Loader.Settings> xbamLoaderSettingsConfiguation = null) {
      Porter = porter;
    }

    /// <summary>
    /// Don't call this in a runtime with an existing default universe.
    /// </summary>
    public void Run() {

    }

    protected virtual void _initXBam() {
      // Configure Settings
      Loader.Settings settings = new() {
        FatalOnCannotInitializeType = true
      };

      /// Load Archetypes
      Loader loader = new(settings);
      loader.Initialize(
        _testUniverse = new Universe(loader, $"test_porter{Porter.DefaultPackageName}")
      );
    }

    protected virtual void _destroyXbam() {
      Archetypes.DefaultUniverse = null;
      Models.DefaultUniverse = null;
      Components.DefaultUniverse = null;

      _testUniverse = null;
    }
  }
}

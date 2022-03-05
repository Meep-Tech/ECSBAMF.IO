using Meep.Tech.Collections.Generic;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Meep.Tech.Data.IO.Tests {

  public partial class PorterTester<TArchetype> where TArchetype : Meep.Tech.Data.Archetype, IPortableArchetype {

    /// <summary>
    /// Make a test using a dummy filesystem and config file.
    /// The config will be applied to the default dummy json file provided,
    /// * Unless there's more than one, and the default chosen is not named _config.json; In that case it will throw a TestFailed exception.
    /// </summary>
    public class ConfigAndDummyFilesTest : Test {
      readonly JObject _config;
      readonly Dictionary<string, object> _options;
      readonly HashSet<string> _dummyFileSystem;
      readonly string _uniqueTestName;
      readonly Func<IEnumerable<TArchetype>, TestResult> _validateCreatedTypes;
      string _testRoot;
      string _outerTestBufferFolder;
      IEnumerable<TArchetype> _createdArchetypes;

      /// <summary>
      /// Set up a Test using a dummy filesystem and provided config override.
      /// </summary>
      /// <param name="dummyFileSystem">Files provided to the Porter.Tester running this, but with the location in the dummy filesystem, starting with ./ or ../ depending. 3 empty parent folders are added to this dummy file system as well. If a json config is provided, this should also include an .json file that was not passed into the test runner to overwrite with the provided json config.</param>
      /// <param name="config">the override for the default json config</param>
      /// <param name="validateCreatedTypes">Used to validate the test and the type, and return the test result</param>
      public ConfigAndDummyFilesTest(string uniqueTestName, HashSet<string> dummyFileSystem, Func<IEnumerable<TArchetype>, TestResult> validateCreatedTypes, JObject config = null, Dictionary<string, object> options = null) {
        _uniqueTestName = uniqueTestName;
        _config = config;
        _options = options ?? new();
        _dummyFileSystem = dummyFileSystem;
        _validateCreatedTypes = validateCreatedTypes;
      }

      protected override void Initalize(PorterTester<TArchetype> testRunner) {
        /// set up the dummy file system:
        _outerTestBufferFolder = Path.Combine(testRunner.TestModsFolder, "___dummy_file_system_for_tests__root", $"__{_uniqueTestName}__outer_folder");
        _testRoot = Path.Combine(_outerTestBufferFolder, "___dummy_outer_parent_folder", "___dummy_middle_parent_folder", "___dummy_inner_parent_folder", $"__{_uniqueTestName}");
        Directory.CreateDirectory(_testRoot);

        List<string> createdDummyFiles = new();
        foreach (string dummyFileLocation in _dummyFileSystem) {
          if (Regex.Matches(dummyFileLocation, "../").Count > 3) {
            throw new ArgumentException($"Dummy file system files cannot have more than 3 ../ occurences, as the dummy file system isn't created any deeper.");
          }

          /// copy the file to where we need from the porter's known dummy files.
          string fileName = Path.GetFileName(dummyFileLocation);
          if (testRunner.TryToGetDummyFile(fileName, out string dummyFileSource)) {
            string createdDummyFile = Path.Combine(_testRoot, dummyFileLocation);
            File.Copy(dummyFileSource, createdDummyFile);
            createdDummyFiles.Add(createdDummyFile);
          }
        }
      }

      protected override TestResult RunTest(PorterTester<TArchetype> testRunner) {
        _createdArchetypes = testRunner.Porter.ImportAndBuildNewArchetypesFromFilesAndFolders(
          Directory.GetFiles(_testRoot), _options 
        );

        return _validateCreatedTypes(_createdArchetypes);
      }

      protected override void DeInitialize(PorterTester<TArchetype> testRunner) {
        _createdArchetypes.ForEach(a => a.Unload());
        Directory.Delete(_outerTestBufferFolder, true);
      }
    }

    protected bool TryToGetDummyFile(string fileName, out string dummyFileFullSystemLocation) {
      throw new NotImplementedException();
    }
  }
}
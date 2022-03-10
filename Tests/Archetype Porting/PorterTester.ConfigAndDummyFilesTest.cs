using Meep.Tech.Collections.Generic;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Meep.Tech.Data.IO.Tests {

  public partial class PorterTester<TArchetype> where TArchetype : Meep.Tech.Data.Archetype, IPortableArchetype {

    /// <summary>
    /// Make a test using a dummy filesystem and config file.
    /// The config will be applied to the default dummy json file provided,
    /// * Unless there's more than one, and the default chosen is not named _config.json; In that case it will throw a TestFailed exception.
    /// </summary>
    public class ImportWithConfigAndDummyFilesTest : Test {
      readonly JObject _config;
      readonly Dictionary<string, object> _options;
      readonly HashSet<string> _dummyFileSystem;
      readonly Func<IEnumerable<TArchetype>, IEnumerable<string>, TestResult> _validateCreatedTypesAndProccessedFiles;
      string _testRoot;
      string _outerTestBufferFolder;
      IEnumerable<TArchetype> _createdArchetypes;

      /// <summary>
      /// Set up a Test using a dummy filesystem and provided config override.
      /// </summary>
      /// <param name="dummyFileSystem">Files provided to the Porter.Tester running this, but with the location in the dummy filesystem, starting with ./ or ../ depending. 3 empty parent folders are added to this dummy file system as well. If a json config is provided, this should also include an .json file that was not passed into the test runner to overwrite with the provided json config.</param>
      /// <param name="config">the override for the default json config</param>
      /// <param name="validateCreatedTypesAndProccessedFiles">Used to validate the test and the type, and return the test result</param>
      public ImportWithConfigAndDummyFilesTest(string uniqueTestName, HashSet<string> dummyFileSystem, Func<IEnumerable<TArchetype>, IEnumerable<string>, TestResult> validateCreatedTypesAndProccessedFiles, JObject config = null, Dictionary<string, object> options = null) {
        UniqueTestName = uniqueTestName;
        _config = config;
        _options = options ?? new();
        _dummyFileSystem = dummyFileSystem;
        _validateCreatedTypesAndProccessedFiles = validateCreatedTypesAndProccessedFiles;
      }

      protected override void Initalize(PorterTester<TArchetype> testRunner) {
        /// set up the dummy file system:
        _outerTestBufferFolder = Path.Combine(testRunner.TestModsFolder, $"__{UniqueTestName}");
        _testRoot = Path.Combine(_outerTestBufferFolder, "__dummy_op", "__dummy_ip", $"__test");
        Directory.CreateDirectory(_testRoot);

        List<string> createdDummyFiles = new();


        HashSet<string> potentialConfigPlaceholderFiles = _config is not null ? _dummyFileSystem.Where(f => Path.GetExtension(f).ToLower() == ".json").ToHashSet() : null;
        foreach (string dummyFileLocation in _dummyFileSystem) {
          if (Regex.Matches(dummyFileLocation, "../").Count > 3) {
            throw new ArgumentException($"Dummy file system files cannot have more than 3 ../ occurences, as the dummy file system isn't created any deeper.");
          }

          /// copy the file to where we need from the porter's known dummy files.
          string fileName = Path.GetFileName(dummyFileLocation);
          if (testRunner.TryToGetDummyFile(fileName, out string dummyFileSource)) {
            string createdDummyFile = Path.GetFullPath(Path.Combine(_testRoot, dummyFileLocation));
            if (!Directory.Exists(createdDummyFile)) {
              Directory.CreateDirectory(Path.GetDirectoryName(createdDummyFile));
            }
            File.Copy(dummyFileSource, createdDummyFile);
            createdDummyFiles.Add(createdDummyFile);
            potentialConfigPlaceholderFiles?.Remove(dummyFileLocation);
          }
        }

        // if we need to add a config.
        if (_config != null) {
          string dummyConfigFileLocation;
          // if we found any .json files that qualify as potential config placeholder files.
          if (potentialConfigPlaceholderFiles.Any()) {
            // get the first one
            dummyConfigFileLocation = potentialConfigPlaceholderFiles.First();
          } else // if we didn't find any placeholders. build the default one in the root
            dummyConfigFileLocation 
              = Path.Combine(_testRoot, ArchetypePorter.DefaultConfigFileName);

          File.WriteAllText(
            (dummyConfigFileLocation = dummyConfigFileLocation.StartsWith('.') 
              ? Path.GetFullPath(Path.Combine(_testRoot, dummyConfigFileLocation)) 
              : dummyConfigFileLocation),
            _config.ToString()
          );
          createdDummyFiles.Add(dummyConfigFileLocation);
        }
      }

      protected override TestResult RunTest(PorterTester<TArchetype> testRunner) {
        _createdArchetypes = testRunner.Porter.ImportAndBuildNewArchetypesFromLooseFilesAndFolders(
          ArchetypePorter.GetValidFlatFilesAndDirectoriesFromDirectory(_testRoot),
          _options,
          out HashSet<string> processedFiles
        );

        return _validateCreatedTypesAndProccessedFiles(_createdArchetypes, processedFiles);
      }

      protected override void DeInitialize(PorterTester<TArchetype> testRunner) {
        _createdArchetypes.ForEach(a => a.Unload());
        Directory.Delete(_outerTestBufferFolder, true);
      }
    }
  }
}
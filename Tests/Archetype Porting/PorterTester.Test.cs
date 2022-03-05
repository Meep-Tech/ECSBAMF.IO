using System;

namespace Meep.Tech.Data.IO.Tests {

  public partial class PorterTester<TArchetype> where TArchetype : Meep.Tech.Data.Archetype, IPortableArchetype {

    public abstract class Test {

      /// <summary>
      /// Run this test on the given porter.
      /// </summary>
      public TestResult RunOn(PorterTester<TArchetype> testRunner) {
        Initalize(testRunner);
        var result = RunTest(testRunner);
        DeInitialize(testRunner);

        return result;
      }

      protected abstract void Initalize(PorterTester<TArchetype> testRunner);

      protected abstract void DeInitialize(PorterTester<TArchetype> testRunner);

      protected abstract TestResult RunTest(PorterTester<TArchetype> testRunner);

      /// <summary>
      /// The result of a Porter test.
      /// </summary>
      public struct TestResult {
        public bool Succeded { get; }
        public string Message { get; }
        public Exception Exception { get; }
        public object Value { get; }

        public TestResult(bool succeded, string message = null, object value = null, Exception exception = null) {
          Succeded = succeded;
          Exception = exception;
          Value = value;
          Message = message ?? exception.Message ?? (succeded ? "Test Succeded!" : "Test Failed!");
        }

        public TestResult(Exception exception) {
          Succeded = false;
          Exception = exception;
          Value = null;
          Message = $"Test Failed: {exception}";
        }
      }
    }
  }
}
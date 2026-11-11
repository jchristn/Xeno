namespace Test.Shared
{
    using System.Collections.Generic;
    using Touchstone.Core;

    /// <summary>
    /// Supplies every shared suite to Touchstone adapters that rebuild cases in the process that runs them (xUnit).
    /// </summary>
    public sealed class XenoTestSuiteProvider : ITestSuiteProvider
    {
        /// <inheritdoc />
        public IReadOnlyList<TestSuiteDescriptor> Suites
        {
            get { return SharedTestSuites.All; }
        }
    }
}

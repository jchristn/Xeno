namespace Test.Nunit
{
    using System.Collections;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Test.Shared;
    using NUnit.Framework;
    using Touchstone.Core;
    using Touchstone.NunitAdapter;

    /// <summary>
    /// NUnit runner for shared Touchstone suites.
    /// </summary>
    [TestFixture]
    public sealed class TouchstoneNunitTests
    {
        /// <summary>
        /// Return shared test cases.
        /// </summary>
        /// <returns>Test cases.</returns>
        public static IEnumerable TestCases()
        {
            return new TouchstoneTestCaseSource(SharedTestSuites.All);
        }

        /// <summary>
        /// Run a shared test case.
        /// </summary>
        /// <param name="testCase">Test case.</param>
        /// <returns>Task.</returns>
        [Test]
        [TestCaseSource(nameof(TestCases))]
        public async Task RunTest(TestCaseDescriptor testCase)
        {
            await testCase.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }
}

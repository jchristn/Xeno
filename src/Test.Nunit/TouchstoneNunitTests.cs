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
            return TouchstoneNunitCases.From(SharedTestSuites.All);
        }

        /// <summary>
        /// Drop any per-test server catalogs created during the run. The backing database is selected
        /// through the XENO_TEST_DB_* environment variables (defaulting to SQLite).
        /// </summary>
        /// <returns>Task.</returns>
        [OneTimeTearDown]
        public async Task DropTestDatabasesAsync()
        {
            await TestDatabase.DropAllAsync().ConfigureAwait(false);
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
            await TouchstoneNunit.ExecuteAsync(testCase, null, null, TestContext.CurrentContext.CancellationToken).ConfigureAwait(false);
        }
    }
}

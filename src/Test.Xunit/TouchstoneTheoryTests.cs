namespace Test.Xunit
{
    using System.Threading;
    using System.Threading.Tasks;
    using global::Test.Shared;
    using global::Touchstone.Core;
    using global::Touchstone.Xunit2Adapter;
    using global::Xunit;
    using global::Xunit.Abstractions;

    /// <summary>
    /// Shared fixture that drops any per-test server catalogs after the suite completes. The backing
    /// database is selected through the XENO_TEST_DB_* environment variables (defaulting to SQLite).
    /// </summary>
    public sealed class DatabaseCleanupFixture : IAsyncLifetime
    {
        /// <inheritdoc />
        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task DisposeAsync()
        {
            await TestDatabase.DropAllAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// xUnit runner for shared Touchstone suites.
    /// </summary>
    public sealed class TouchstoneTheoryTests : IClassFixture<DatabaseCleanupFixture>
    {
        private readonly ITestOutputHelper _Output;

        /// <summary>
        /// Create the runner.
        /// </summary>
        /// <param name="output">Test output.</param>
        public TouchstoneTheoryTests(ITestOutputHelper output)
        {
            _Output = output;
        }

        /// <summary>
        /// Return test cases.
        /// </summary>
        /// <returns>Theory data.</returns>
        public static TheoryData<TouchstoneXunit2Case> TestCases()
        {
            return TouchstoneXunit2Cases.From<XenoTestSuiteProvider>();
        }

        /// <summary>
        /// Run a shared test case.
        /// </summary>
        /// <param name="testCase">Test case.</param>
        /// <returns>Task.</returns>
        [Theory]
        [MemberData(nameof(TestCases))]
        public Task RunTest(TouchstoneXunit2Case testCase)
        {
            return TouchstoneXunit2.ExecuteAsync(testCase, _Output, CancellationToken.None);
        }
    }
}

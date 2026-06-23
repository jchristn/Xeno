namespace Test.Xunit
{
    using System.Threading;
    using System.Threading.Tasks;
    using global::Test.Shared;
    using global::Touchstone.Core;
    using global::Touchstone.XunitAdapter;
    using global::Xunit;

    /// <summary>
    /// xUnit runner for shared Touchstone suites.
    /// </summary>
    public sealed class TouchstoneTheoryTests
    {
        /// <summary>
        /// Return test cases.
        /// </summary>
        /// <returns>Theory data.</returns>
        public static TheoryData<TestCaseDescriptor> TestCases()
        {
            return new TouchstoneTheoryData(SharedTestSuites.All);
        }

        /// <summary>
        /// Run a shared test case.
        /// </summary>
        /// <param name="testCase">Test case.</param>
        /// <returns>Task.</returns>
        [Theory]
        [MemberData(nameof(TestCases))]
        public async Task RunTest(TestCaseDescriptor testCase)
        {
            await testCase.ExecuteAsync(CancellationToken.None);
        }
    }
}

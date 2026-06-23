namespace Test.Automated
{
    using System.Threading.Tasks;
    using global::Test.Shared;
    using Touchstone.Cli;

    /// <summary>
    /// Automated test runner.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Run shared Touchstone suites.
        /// </summary>
        /// <param name="args">Arguments.</param>
        /// <returns>Exit code.</returns>
        public static async Task<int> Main(string[] args)
        {
            string? resultsPath = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--results" && i + 1 < args.Length)
                {
                    resultsPath = args[i + 1];
                    break;
                }
            }

            return await ConsoleRunner.RunAsync(SharedTestSuites.All, resultsPath: resultsPath).ConfigureAwait(false);
        }
    }
}

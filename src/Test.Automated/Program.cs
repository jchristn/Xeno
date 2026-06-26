namespace Test.Automated
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using global::Test.Shared;
    using Touchstone.Cli;
    using Touchstone.Core;

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
            string? suiteId = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--results" && i + 1 < args.Length)
                {
                    resultsPath = args[i + 1];
                    i++;
                    continue;
                }

                if (args[i] == "--suite" && i + 1 < args.Length)
                {
                    suiteId = args[i + 1];
                    i++;
                    continue;
                }
            }

            IReadOnlyList<TestSuiteDescriptor> suites = SharedTestSuites.All;
            if (!String.IsNullOrWhiteSpace(suiteId))
            {
                suites = suites.Where(suite => String.Equals(suite.SuiteId, suiteId, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            return await ConsoleRunner.RunAsync(suites, resultsPath: resultsPath).ConfigureAwait(false);
        }
    }
}

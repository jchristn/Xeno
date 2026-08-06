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
            string? dbProvider = null;
            string? dbHost = null;
            int dbPort = 0;
            string? dbName = null;
            string? dbUser = null;
            string? dbPassword = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--results" && i + 1 < args.Length) { resultsPath = args[++i]; continue; }
                if (args[i] == "--suite" && i + 1 < args.Length) { suiteId = args[++i]; continue; }
                if (args[i] == "--db-provider" && i + 1 < args.Length) { dbProvider = args[++i]; continue; }
                if (args[i] == "--db-host" && i + 1 < args.Length) { dbHost = args[++i]; continue; }
                if (args[i] == "--db-port" && i + 1 < args.Length) { dbPort = Int32.TryParse(args[++i], out int parsedPort) ? parsedPort : 0; continue; }
                if (args[i] == "--db-name" && i + 1 < args.Length) { dbName = args[++i]; continue; }
                if (args[i] == "--db-user" && i + 1 < args.Length) { dbUser = args[++i]; continue; }
                if (args[i] == "--db-password" && i + 1 < args.Length) { dbPassword = args[++i]; continue; }
            }

            // When a provider is supplied on the command line the whole shared suite runs against it;
            // otherwise TestDatabase resolves lazily from environment variables and defaults to SQLite.
            if (!String.IsNullOrWhiteSpace(dbProvider))
            {
                TestDatabase.Configure(dbProvider, dbHost, dbPort, dbName, dbUser, dbPassword);
            }

            IReadOnlyList<TestSuiteDescriptor> suites = SharedTestSuites.All;
            if (!String.IsNullOrWhiteSpace(suiteId))
            {
                suites = suites.Where(suite => String.Equals(suite.SuiteId, suiteId, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            Console.WriteLine("Test database provider: " + TestDatabase.Provider);
            try
            {
                return await ConsoleRunner.RunAsync(suites, resultsPath: resultsPath).ConfigureAwait(false);
            }
            finally
            {
                await TestDatabase.DropAllAsync().ConfigureAwait(false);
            }
        }
    }
}

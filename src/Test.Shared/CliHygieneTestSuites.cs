namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using Xeno.Client;
    using Xeno.Client.Services;
    using Xeno.Core.Settings;

    /// <summary>
    /// Command-line and startup output: secrets stay masked, status reflects a stopped worker, errors stay off stdout,
    /// and the server says when it wrote default settings.
    /// </summary>
    public static class CliHygieneTestSuites
    {
        /// <summary>
        /// Output hygiene suite.
        /// </summary>
        /// <returns>Suite.</returns>
        public static TestSuiteDescriptor CliHygieneSuite()
        {
            const string suite = "CliHygiene";
            return new TestSuiteDescriptor(suite, "CLI and startup output", new List<TestCaseDescriptor>
            {
                new TestCaseDescriptor(suite, "AuthDescriptionMasksTheAccessKey", "The auth description never contains the access key", executeAsync: delegate
                {
                    const string key = "xak_abcdefghijkl1234";
                    ClientSettings settings = new ClientSettings();
                    settings.Server.AccessKey = key;
                    string described = ServerEndpoints.DescribeAuth(settings);
                    Check(!described.Contains(key, StringComparison.Ordinal) && described.Contains("...1234", StringComparison.Ordinal), "The key was not masked: " + described.Length);
                    settings.Server.AccessKey = "default";
                    Check(!ServerEndpoints.DescribeAuth(settings).Contains("default", StringComparison.Ordinal), "A short key should not be shown at all.");
                    return Task.CompletedTask;
                }),
                new TestCaseDescriptor(suite, "StoppedWorkerIsNotBackground", "A recorded background worker that has exited reads as not background", executeAsync: delegate
                {
                    using (ClientHomeScope home = new ClientHomeScope())
                    {
                        new ClientConnectionState { IsBackground = true, ProcessId = Int32.MaxValue }.Save();
                        Check(!ClientConnectionState.Load().IsBackground, "A stopped worker still reads as background.");
                        new ClientConnectionState { IsBackground = true, ProcessId = Environment.ProcessId }.Save();
                        Check(ClientConnectionState.Load().IsBackground, "A running worker should read as background.");
                    }

                    return Task.CompletedTask;
                }),
                new TestCaseDescriptor(suite, "ErrorsGoToStandardError", "A failing command leaves stdout free of error text and reports on stderr", executeAsync: async delegate
                {
                    using (ClientHomeScope home = new ClientHomeScope())
                    {
                        LocalUsageTestSuites.CliResult result = await LocalUsageTestSuites.RunCliAsync(home.Directory, "usage", "--json", "--range", "bogus").ConfigureAwait(false);
                        Check(result.ExitCode != 0, "The command should fail.");
                        Check(!result.Output.Contains("Error:", StringComparison.Ordinal), "stdout has error text: " + result.Output);
                        Check(result.Error.Contains("Error: --range must be", StringComparison.Ordinal), "stderr has no error: " + result.Error);
                    }
                }),
                new TestCaseDescriptor(suite, "ServerNamesCreatedSettings", "The default settings notice names the file and the listening address", executeAsync: delegate
                {
                    string notice = Xeno.Server.Program.DescribeCreatedSettings("created.json", new XenoSettings());
                    Check(notice.Contains(Path.GetFullPath("created.json"), StringComparison.Ordinal) && notice.Contains("0.0.0.0:9000", StringComparison.Ordinal), "The notice is incomplete: " + notice);
                    return Task.CompletedTask;
                })
            });
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}

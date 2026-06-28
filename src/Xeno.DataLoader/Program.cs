namespace Xeno.DataLoader
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Xeno synthetic data loader entry point.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Run the synthetic data loader.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        /// <returns>Process exit code.</returns>
        public static async Task<int> Main(string[] args)
        {
            using (CancellationTokenSource cancellation = new CancellationTokenSource())
            {
                Console.CancelKeyPress += (_, eventArgs) =>
                {
                    eventArgs.Cancel = true;
                    cancellation.Cancel();
                };

                try
                {
                    DataLoaderOptions options = DataLoaderOptionsParser.Parse(args, DateTime.UtcNow);
                    if (options.ShowHelp)
                    {
                        Console.WriteLine(DataLoaderHelp.Text);
                        return 0;
                    }

                    SyntheticDataLoader loader = new SyntheticDataLoader(options);
                    SyntheticDataLoadResult result = await loader.RunAsync(cancellation.Token).ConfigureAwait(false);
                    Console.WriteLine("Synthetic data load complete.");
                    Console.WriteLine("  Range UTC:          " + result.FromUtc.ToString("O") + " to " + result.ToUtc.ToString("O"));
                    Console.WriteLine("  Synthetic clients:  " + result.ClientsCreated.ToString());
                    Console.WriteLine("  Requests:           " + result.RequestHistoryEntriesCreated.ToString());
                    Console.WriteLine("  Host telemetry:     " + result.HostTelemetrySamplesCreated.ToString());
                    Console.WriteLine("  Provider telemetry: " + result.ProviderTelemetrySamplesCreated.ToString());
                    Console.WriteLine("  Attempts:           " + result.CollectionAttemptsCreated.ToString());
                    Console.WriteLine("  Backup:             " + (String.IsNullOrWhiteSpace(result.BackupPath) ? "(none)" : result.BackupPath));
                    return 0;
                }
                catch (OperationCanceledException)
                {
                    Console.Error.WriteLine("Synthetic data load canceled.");
                    return 2;
                }
                catch (ArgumentException ex)
                {
                    Console.Error.WriteLine(ex.Message);
                    Console.Error.WriteLine();
                    Console.Error.WriteLine(DataLoaderHelp.Text);
                    return 1;
                }
                catch (InvalidOperationException ex)
                {
                    Console.Error.WriteLine(ex.Message);
                    return 1;
                }
            }
        }
    }
}

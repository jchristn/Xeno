namespace Xeno.Server
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Serialization;
    using Xeno.Core.Settings;

    /// <summary>
    /// Xeno server entry point.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Start the Xeno server.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        /// <returns>Task.</returns>
        public static async Task Main(string[] args)
        {
            string settingsFile = args.Length > 0 ? args[0] : Environment.GetEnvironmentVariable("XENO_SETTINGS_FILE") ?? "xeno.json";
            XenoSettings settings = LoadSettings(settingsFile);

            using (CancellationTokenSource tokenSource = new CancellationTokenSource())
            {
                Console.CancelKeyPress += delegate(object? sender, ConsoleCancelEventArgs eventArgs)
                {
                    eventArgs.Cancel = true;
                    tokenSource.Cancel();
                };

                XenoServer server = new XenoServer(settings);
                await server.StartAsync(tokenSource.Token).ConfigureAwait(false);
            }
        }

        private static XenoSettings LoadSettings(string filename)
        {
            if (!File.Exists(filename))
            {
                XenoSettings created = new XenoSettings();
                File.WriteAllText(filename, JsonUtil.Serialize(created, true));
                return created;
            }

            string json = File.ReadAllText(filename);
            return JsonUtil.Deserialize<XenoSettings>(json) ?? new XenoSettings();
        }
    }
}

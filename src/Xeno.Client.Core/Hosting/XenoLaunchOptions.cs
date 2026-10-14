namespace Xeno.Client.Hosting
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Client.Presentation;
    using Xeno.Client.Services;

    /// <summary>
    /// Command-line options shared by the desktop app (xeno-desktop) and the terminal app (xeno tui): the page to open,
    /// the client home directory, and the Xeno server to point at. Both apps parse and apply them through this class so
    /// they behave the same.
    /// </summary>
    public sealed class XenoLaunchOptions
    {
        #region Public-Members

        /// <summary>
        /// Usage lines for the shared options, for help output.
        /// </summary>
        public static readonly IReadOnlyList<string> Usage = new List<string>
        {
            "  --page <page>     Open a page: overview, models, providers, requests, logs,",
            "                    hosttelemetry, providertelemetry, server, settings.",
            "  --server <url>    Point this client at a Xeno server and save it to client.json:",
            "                    cloud (the default Xeno cloud server), a ws:// or wss:// URL,",
            "                    an http:// or https:// URL, or host[:port] (port 9000 if omitted).",
            "  --home <dir>      Use a different client home directory instead of ~/.xeno",
            "                    (same as XENO_CLIENT_HOME). Keep one home per server."
        };

        /// <summary>
        /// Page to open, or null for the last page used.
        /// </summary>
        public XenoPageEnum? StartPage { get; set; } = null;

        /// <summary>
        /// Server value from --server, or null to keep the configured server.
        /// </summary>
        public string? Server { get; set; } = null;

        /// <summary>
        /// Client home directory from --home, or null for the default.
        /// </summary>
        public string? Home { get; set; } = null;

        /// <summary>
        /// True when help was requested.
        /// </summary>
        public bool ShowHelp { get; set; } = false;

        /// <summary>
        /// Parse error, or null when the arguments are valid.
        /// </summary>
        public string? Error { get; set; } = null;

        /// <summary>
        /// Arguments this parser did not recognize, in order.
        /// </summary>
        public List<string> Remaining { get; set; } = new List<string>();

        #endregion

        #region Public-Methods

        /// <summary>
        /// Parse arguments.
        /// </summary>
        /// <param name="args">Arguments.</param>
        /// <param name="startIndex">Index of the first argument to parse.</param>
        /// <returns>Options. Check <see cref="Error"/> before using them.</returns>
        public static XenoLaunchOptions Parse(IReadOnlyList<string>? args, int startIndex = 0)
        {
            XenoLaunchOptions options = new XenoLaunchOptions();
            if (args == null) return options;
            for (int i = Math.Max(0, startIndex); i < args.Count; i++)
            {
                string arg = args[i];
                if (arg == "-h" || arg == "--help" || arg == "-?" || String.Equals(arg, "help", StringComparison.OrdinalIgnoreCase))
                {
                    options.ShowHelp = true;
                    continue;
                }

                if (String.Equals(arg, "--page", StringComparison.OrdinalIgnoreCase)
                    || String.Equals(arg, "--server", StringComparison.OrdinalIgnoreCase)
                    || String.Equals(arg, "--home", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 >= args.Count || String.IsNullOrWhiteSpace(args[i + 1]))
                    {
                        options.Error = arg + " requires a value.";
                        return options;
                    }

                    string value = args[++i];
                    if (String.Equals(arg, "--page", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!Enum.TryParse(value, true, out XenoPageEnum page) || !Enum.IsDefined(typeof(XenoPageEnum), page))
                        {
                            options.Error = "Unknown page: " + value + ". Pages: " + String.Join(", ", Enum.GetNames(typeof(XenoPageEnum))).ToLowerInvariant() + ".";
                            return options;
                        }

                        options.StartPage = page;
                    }
                    else if (String.Equals(arg, "--server", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            ServerTargets.Resolve(value);
                        }
                        catch (ArgumentException ex)
                        {
                            options.Error = ex.Message;
                            return options;
                        }

                        options.Server = value;
                    }
                    else
                    {
                        options.Home = value;
                    }

                    continue;
                }

                options.Remaining.Add(arg);
            }

            return options;
        }

        /// <summary>
        /// Apply --home by setting XENO_CLIENT_HOME for this process and the processes it starts (the worker, the tray
        /// agent, and the terminal app). Call before loading any settings.
        /// </summary>
        public void ApplyHome()
        {
            if (String.IsNullOrWhiteSpace(Home)) return;
            string full = Path.GetFullPath(Environment.ExpandEnvironmentVariables(ExpandHome(Home.Trim())));
            Directory.CreateDirectory(full);
            Environment.SetEnvironmentVariable(ClientHomeVariable, full);
        }

        /// <summary>
        /// Apply --server by saving it to client.json.
        /// </summary>
        /// <param name="workspace">Workspace.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A message describing the change, or null when no server was given.</returns>
        /// <exception cref="ArgumentNullException">Workspace is null.</exception>
        /// <exception cref="ArgumentException">The server value is invalid.</exception>
        public async Task<string?> ApplyServerAsync(XenoWorkspace workspace, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(workspace);
            if (String.IsNullOrWhiteSpace(Server)) return null;
            ServerTarget before = ServerTargets.FromSettings(workspace.LoadSettings());
            ServerTarget after = await workspace.UseServerAsync(Server, token).ConfigureAwait(false);
            if (String.Equals(before.WebSocketUrl, after.WebSocketUrl, StringComparison.OrdinalIgnoreCase) && String.Equals(before.RestUrl, after.RestUrl, StringComparison.OrdinalIgnoreCase))
            {
                return "Using " + after.Label + " (" + after.WebSocketUrl + ").";
            }

            ClientStatusSnapshot status = workspace.GetStatus();
            return "Pointed this client at " + after.Label + " (" + after.WebSocketUrl + ")."
                + (status.Running ? " Restart the client to connect to it." : String.Empty)
                + (status.HasCredential ? " Sign in again if your access key belongs to a different server." : String.Empty);
        }

        /// <summary>
        /// Arguments that pass the client home directory on to another Xeno app, or an empty list when the default home
        /// is in use.
        /// </summary>
        /// <returns>Arguments.</returns>
        public static List<string> ForwardHomeArguments()
        {
            List<string> args = new List<string>();
            string? home = Environment.GetEnvironmentVariable(ClientHomeVariable);
            if (!String.IsNullOrWhiteSpace(home))
            {
                args.Add("--home");
                args.Add(home);
            }

            return args;
        }

        #endregion

        #region Private-Members

        private const string ClientHomeVariable = "XENO_CLIENT_HOME";

        private static string ExpandHome(string path)
        {
            if (path == "~" || path.StartsWith("~/", StringComparison.Ordinal) || path.StartsWith("~\\", StringComparison.Ordinal))
            {
                string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return path.Length == 1 ? profile : Path.Combine(profile, path.Substring(2));
            }

            return path;
        }

        #endregion
    }
}

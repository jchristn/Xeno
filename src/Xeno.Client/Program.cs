namespace Xeno.Client
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Net.WebSockets;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Client.Hosting;
    using Xeno.Client.LocalDashboard;
    using Xeno.Client.LocalInference;
    using Xeno.Client.Presentation;
    using Xeno.Client.Routing;
    using Xeno.Client.Services;
    using Xeno.Client.Telemetry.Providers;
    using Xeno.Client.Telemetry.Services;
    using Xeno.Core.Enums;
    using Xeno.Core.Helpers;
    using Xeno.Core.Models;
    using Xeno.Core.Models.Telemetry;
    using Xeno.Core.Serialization;
    using Xeno.Tui;

    /// <summary>
    /// Xeno client CLI entry point.
    /// </summary>
    public static partial class Program
    {
        private const string ClientVersion = "0.2.0";
        private const int DefaultConnectTimeoutMs = 30000;
        private const int DefaultStopTimeoutMs = 10000;
        private const int DefaultPingCount = 5;
        private const int DefaultPingTimeoutMs = 5000;
        private const int DefaultPingDelayMs = 1000;
        private static bool _UseColors = true;
        private static bool _UseJson = false;
        private static bool _WorkerMode = false;
        private static TimestampedLogWriter? _WorkerOutputLogWriter = null;
        private static readonly SemaphoreSlim _SendLock = new SemaphoreSlim(1, 1);
        private static readonly ConcurrentDictionary<string, CancellationTokenSource> _RequestCancellationSources = new ConcurrentDictionary<string, CancellationTokenSource>(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, string> _RequestCancellationReasons = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, TaskCompletionSource<ServerPerformanceTestRelayResponseMessage>> _PerformanceRelayResponses = new ConcurrentDictionary<string, TaskCompletionSource<ServerPerformanceTestRelayResponseMessage>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Run the Xeno client CLI.
        /// </summary>
        /// <param name="args">Arguments.</param>
        /// <returns>Exit code.</returns>
        public static async Task<int> Main(string[] args)
        {
            // The terminal app owns the whole screen, so it runs before console output wrapping and global options.
            if (args.Length > 0 && String.Equals(args[0], "tui", StringComparison.OrdinalIgnoreCase))
            {
                return await HandleTuiAsync(args).ConfigureAwait(false);
            }

            // MCP over stdio owns stdout, so it runs before console output wrapping too.
            if (args.Length > 0 && String.Equals(args[0], "mcp", StringComparison.OrdinalIgnoreCase))
            {
                return await HandleMcpAsync(args).ConfigureAwait(false);
            }

            TrailingBlankLineTextWriter output = new TrailingBlankLineTextWriter(Console.Out);
            Console.SetOut(output);
            try
            {
                return await RunAsync(args).ConfigureAwait(false);
            }
            finally
            {
                if (!_UseJson && !_WorkerMode)
                {
                    output.EnsureTrailingBlankLine();
                }

                if (Object.ReferenceEquals(Console.Out, output))
                {
                    Console.SetOut(output.Inner);
                }
            }
        }

        private static async Task<int> HandleTuiAsync(string[] args)
        {
            XenoLaunchOptions launch = XenoLaunchOptions.Parse(args, 1);
            if (launch.ShowHelp)
            {
                PrintTuiHelp();
                return 0;
            }

            if (launch.Error != null || launch.Remaining.Count > 0)
            {
                Console.Error.WriteLine(launch.Error ?? "Unknown tui option: " + launch.Remaining[0]);
                PrintTuiHelp();
                return 1;
            }

            if (Console.IsOutputRedirected || Console.IsInputRedirected)
            {
                Console.Error.WriteLine("xeno tui needs an interactive terminal.");
                return 1;
            }

            launch.ApplyHome();
            XenoAppLauncher.EnsureTrayRunning();
            TuiStartOptions options = new TuiStartOptions { StartPage = launch.StartPage };
            if (launch.Server != null)
            {
                using (XenoWorkspace workspace = new XenoWorkspace())
                {
                    options.StartupMessage = await launch.ApplyServerAsync(workspace, CancellationToken.None).ConfigureAwait(false);
                }
            }

            return await XenoTuiApp.RunConsoleAsync(options, CancellationToken.None).ConfigureAwait(false);
        }

        private static void PrintTuiHelp()
        {
            Console.WriteLine("Usage: xeno tui [--page <page>] [--server <url>] [--home <dir>]");
            Console.WriteLine();
            Console.WriteLine("Open the Xeno terminal app: client status, models served here and on the Xeno network,");
            Console.WriteLine("providers, requests, logs, telemetry, server account, and settings.");
            Console.WriteLine();
            Console.WriteLine("Options:");
            foreach (string line in XenoLaunchOptions.Usage) Console.WriteLine(line);
            Console.WriteLine();
            Console.WriteLine("Keys: F10 menu, Ctrl+K command palette, F1 help, Tab next pane, Ctrl+Q quit.");
        }

        private static async Task<int> RunAsync(string[] args)
        {
            try
            {
                string[] earlyArgs = ApplyGlobalOptions(args, null);
                if (earlyArgs.Length > 0 && String.Equals(earlyArgs[0], "reset", StringComparison.OrdinalIgnoreCase))
                {
                    return HandleReset(earlyArgs);
                }

                ClientSettings settings = ClientSettings.Load();
                args = ApplyGlobalOptions(args, settings);

                if (args.Length == 0 || args[0] == "help" || args[0] == "--help" || args[0] == "-h")
                {
                    PrintHelp();
                    return 0;
                }

                string command = args[0].ToLowerInvariant();

                try
                {
                    switch (command)
                    {
                        case "version":
                            if (_UseJson) WriteJson(new { version = ClientVersion });
                            else WriteInfo("xeno " + ClientVersion);
                            return 0;
                        case "ping":
                            return await HandlePingAsync(args, settings).ConfigureAwait(false);
                        case "init":
                            settings.Save();
                            WriteSuccess("Created " + ClientSettings.SettingsFile);
                            return 0;
                        case "reset":
                            return HandleReset(args);
                        case "config":
                            return HandleConfig(args, settings);
                        case "server":
                            return HandleServer(args, settings);
                        case "auth":
                            return HandleAuth(args, settings);
                        case "set-auth":
                            return HandleSetAuth(args, settings);
                        case "login":
                            return await HandleLoginAsync(args, settings).ConfigureAwait(false);
                        case "provider":
                        case "runner":
                            return await HandleRunnerAsync(args, settings).ConfigureAwait(false);
                        case "model":
                            return HandleModel(args, settings);
                        case "inference":
                            return await HandleInferenceAsync(args, settings).ConfigureAwait(false);
                        case "chat":
                            return await HandleChatAsync(args).ConfigureAwait(false);
                        case "usage":
                            return await HandleUsageAsync(args).ConfigureAwait(false);
                        case "inventory":
                            if (_UseJson) WriteJson(settings.BuildInventory());
                            else PrintInventory(settings);
                            return 0;
                        case "reload":
                            return HandleReload(args);
                        case "status":
                            PrintStatus(settings);
                            return 0;
                        case "connect":
                        case "start":
                            return await HandleConnectAsync(args, settings).ConfigureAwait(false);
                        case "restart":
                            return await HandleRestartAsync(args, settings).ConfigureAwait(false);
                        case "logs":
                            return await HandleLogsAsync(args, CancellationToken.None).ConfigureAwait(false);
                        case "dashboard":
                            return HandleDashboard(args, settings);
                        case "stop":
                        case "disconnect":
                            return await HandleStopAsync(args).ConfigureAwait(false);
                        default:
                            WriteError("Unknown command '" + args[0] + "'.");
                            PrintHelp();
                            return 1;
                    }
                }
                catch (HttpRequestException ex)
                {
                    WriteError("Could not reach the Xeno server: " + ex.Message);
                    return 1;
                }
                catch (InvalidDataException ex)
                {
                    WriteError("Could not parse the server response: " + ex.Message);
                    return 1;
                }
                catch (Exception ex)
                {
                    WriteError(ex.Message);
                    return 1;
                }
            }
            catch (InvalidDataException ex)
            {
                WriteError("Could not read " + ClientSettings.SettingsFile + ": " + ex.Message);
                return 1;
            }
            catch (Exception ex)
            {
                WriteError(ex.Message);
                return 1;
            }
        }

        private static async Task<int> HandleConnectAsync(string[] args, ClientSettings settings)
        {
            if (args.Length > 1 && IsHelp(args[1]))
            {
                PrintConnectHelp();
                return 0;
            }

            bool foreground = false;
            bool worker = false;
            int timeoutMs = DefaultConnectTimeoutMs;
            for (int i = 1; i < args.Length; i++)
            {
                if (String.Equals(args[i], "--foreground", StringComparison.OrdinalIgnoreCase))
                {
                    foreground = true;
                    continue;
                }

                if (String.Equals(args[i], "--worker", StringComparison.OrdinalIgnoreCase))
                {
                    foreground = true;
                    worker = true;
                    continue;
                }

                if (String.Equals(args[i], "--timeout", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 >= args.Length || !Int32.TryParse(args[i + 1], out timeoutMs) || timeoutMs < 1000)
                    {
                        WriteError("--timeout requires a value in milliseconds greater than or equal to 1000.");
                        return 1;
                    }

                    i++;
                    continue;
                }

                WriteError("Unknown connect option: " + args[i]);
                PrintConnectHelp();
                return 1;
            }

            if (foreground)
            {
                _WorkerMode = worker;
                if (_WorkerMode) ConfigureWorkerLogging();

                // Starting the client from a terminal also brings up the Xeno Agent tray icon; the background worker never
                // does, so quitting the agent sticks. The agent waits a few seconds before checking whether to connect.
                if (!worker) XenoAppLauncher.EnsureTrayRunning();
                await RunForegroundConnectAsync(settings, worker, CancellationToken.None).ConfigureAwait(false);
                return 0;
            }

            int connected = await StartBackgroundConnectAsync(settings, timeoutMs, CancellationToken.None).ConfigureAwait(false);
            XenoAppLauncher.EnsureTrayRunning();
            return connected;
        }

        private static async Task<int> HandleRestartAsync(string[] args, ClientSettings settings)
        {
            if (args.Length > 1 && IsHelp(args[1]))
            {
                PrintRestartHelp();
                return 0;
            }

            bool force = false;
            int timeoutMs = DefaultConnectTimeoutMs;
            for (int i = 1; i < args.Length; i++)
            {
                if (String.Equals(args[i], "--force", StringComparison.OrdinalIgnoreCase))
                {
                    force = true;
                    continue;
                }

                if (String.Equals(args[i], "--timeout", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 >= args.Length || !Int32.TryParse(args[i + 1], out timeoutMs) || timeoutMs < 1000)
                    {
                        WriteError("--timeout requires a value in milliseconds greater than or equal to 1000.");
                        return 1;
                    }

                    i++;
                    continue;
                }

                WriteError("Unknown restart option: " + args[i]);
                PrintRestartHelp();
                return 1;
            }

            if (_UseJson)
            {
                WriteJson(new
                {
                    restarted = false,
                    error = "JsonRestartUnsupported",
                    message = "'xeno restart' does not support --json. Run 'xeno stop' and 'xeno start' separately for machine-readable output."
                });
                return 1;
            }

            string[] stopArgs = force ? new[] { "stop", "--force" } : new[] { "stop" };
            int stopExitCode = await HandleStopAsync(stopArgs).ConfigureAwait(false);
            if (stopExitCode != 0) return stopExitCode;

            return await HandleConnectAsync(new[] { "start", "--timeout", timeoutMs.ToString(CultureInfo.InvariantCulture) }, settings).ConfigureAwait(false);
        }

        private static async Task<int> HandleLogsAsync(string[] args, CancellationToken token)
        {
            if (args.Length > 1 && IsHelp(args[1]))
            {
                PrintLogsHelp();
                return 0;
            }

            bool follow = false;
            int tailLines = 100;
            string logFile = ClientConnectionState.LogFile;
            for (int i = 1; i < args.Length; i++)
            {
                if (String.Equals(args[i], "-f", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(args[i], "--follow", StringComparison.OrdinalIgnoreCase))
                {
                    follow = true;
                    continue;
                }

                if (String.Equals(args[i], "--errors", StringComparison.OrdinalIgnoreCase))
                {
                    logFile = ClientConnectionState.ErrorLogFile;
                    continue;
                }

                if (String.Equals(args[i], "--tail", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 >= args.Length || !Int32.TryParse(args[i + 1], out tailLines) || tailLines < 0)
                    {
                        WriteError("--tail requires a non-negative line count.");
                        return 1;
                    }

                    i++;
                    continue;
                }

                WriteError("Unknown logs option: " + args[i]);
                PrintLogsHelp();
                return 1;
            }

            if (_UseJson)
            {
                WriteJson(new
                {
                    logFile,
                    exists = File.Exists(logFile),
                    follow,
                    tailLines
                });
                return 0;
            }

            if (!File.Exists(logFile))
            {
                WriteInfo("No Xeno client log file exists yet: " + logFile);
                return 0;
            }

            foreach (string line in await ClientLogReader.ReadTailAsync(logFile, tailLines, null, token).ConfigureAwait(false))
            {
                Console.WriteLine(line);
            }

            if (follow) await ClientLogReader.FollowAsync(logFile, Console.WriteLine, token).ConfigureAwait(false);
            return 0;
        }

        private static async Task<int> HandlePingAsync(string[] args, ClientSettings settings)
        {
            if (args.Length > 1 && IsHelp(args[1]))
            {
                PrintPingHelp();
                return 0;
            }

            int count = DefaultPingCount;
            int timeoutMs = DefaultPingTimeoutMs;
            string? serverUrl = null;
            for (int i = 1; i < args.Length; i++)
            {
                if (String.Equals(args[i], "-n", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(args[i], "--count", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 >= args.Length || !Int32.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out count) || count < 1 || count > 1000)
                    {
                        WriteError("-n/--count requires a value from 1 through 1000.");
                        return 1;
                    }

                    i++;
                    continue;
                }

                if (String.Equals(args[i], "-w", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(args[i], "--timeout", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 >= args.Length || !Int32.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out timeoutMs) || timeoutMs < 100 || timeoutMs > 600000)
                    {
                        WriteError("-w/--timeout requires a value from 100 through 600000 milliseconds.");
                        return 1;
                    }

                    i++;
                    continue;
                }

                if (String.Equals(args[i], "--server", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 >= args.Length || String.IsNullOrWhiteSpace(args[i + 1]))
                    {
                        WriteError("--server requires a REST server URL.");
                        return 1;
                    }

                    serverUrl = args[++i];
                    continue;
                }

                WriteError("Unknown ping option: " + args[i]);
                PrintPingHelp();
                return 1;
            }

            ClientSettings pingSettings = settings;
            if (!String.IsNullOrWhiteSpace(serverUrl))
            {
                pingSettings.Server.RestUrl = serverUrl;
            }

            Uri endpoint = ServerEndpoints.BuildRestUri(pingSettings, "/v1.0/api/ping");
            string target = endpoint.Host;
            List<long> responseTimes = new List<long>();
            List<object> attempts = new List<object>();
            int received = 0;

            if (!_UseJson)
            {
                Console.WriteLine("Pinging " + target + " [" + endpoint.GetLeftPart(UriPartial.Authority) + "] with HTTP heartbeat:");
                Console.WriteLine();
            }

            using (XenoServerClient server = new XenoServerClient())
            {
                for (int sequence = 1; sequence <= count; sequence++)
                {
                    PingAttemptResult attempt = await server.PingAsync(pingSettings, sequence, timeoutMs, CancellationToken.None).ConfigureAwait(false);
                    if (attempt.Success)
                    {
                        received++;
                        responseTimes.Add(attempt.ElapsedMs);
                        if (!_UseJson)
                        {
                            Console.WriteLine("Reply from " + target + ": bytes=" + attempt.Bytes.ToString(CultureInfo.InvariantCulture) + " time=" + FormatPingTime(attempt.ElapsedMs) + " seq=" + sequence.ToString(CultureInfo.InvariantCulture) + " status=" + DisplayValue(attempt.Status, "ok") + " serverUtc=" + DisplayValue(attempt.ServerUtc, "unknown"));
                        }
                    }
                    else if (!_UseJson)
                    {
                        if (attempt.Error == "Timeout") Console.WriteLine("Request timed out.");
                        else if (attempt.Error != null) Console.WriteLine("Request failed: " + attempt.Error);
                        else Console.WriteLine("Reply from " + target + ": HTTP " + attempt.StatusCode.ToString(CultureInfo.InvariantCulture) + " " + attempt.ReasonPhrase + " time=" + FormatPingTime(attempt.ElapsedMs));
                    }

                    if (attempt.Error == null)
                    {
                        attempts.Add(new
                        {
                            sequence,
                            success = attempt.Success,
                            statusCode = attempt.StatusCode,
                            bytes = attempt.Bytes,
                            elapsedMs = attempt.ElapsedMs,
                            status = attempt.Status,
                            serverUtc = attempt.ServerUtc
                        });
                    }
                    else
                    {
                        attempts.Add(new
                        {
                            sequence,
                            success = false,
                            statusCode = 0,
                            bytes = 0,
                            elapsedMs = attempt.ElapsedMs,
                            status = String.Empty,
                            serverUtc = String.Empty,
                            error = attempt.Error
                        });
                    }

                    if (sequence < count) await Task.Delay(DefaultPingDelayMs).ConfigureAwait(false);
                }
            }

            int lost = count - received;
            int lossPercent = (int)Math.Round((double)lost * 100.0 / count);
            long minimum = responseTimes.Count == 0 ? 0 : responseTimes.Min();
            long maximum = responseTimes.Count == 0 ? 0 : responseTimes.Max();
            long average = responseTimes.Count == 0 ? 0 : (long)Math.Round(responseTimes.Average());

            if (_UseJson)
            {
                WriteJson(new
                {
                    target,
                    endpoint = endpoint.ToString(),
                    sent = count,
                    received,
                    lost,
                    lossPercent,
                    minimumMs = minimum,
                    maximumMs = maximum,
                    averageMs = average,
                    attempts
                });
                return received > 0 ? 0 : 1;
            }

            Console.WriteLine();
            Console.WriteLine("Ping statistics for " + target + ":");
            Console.WriteLine("    Packets: Sent = " + count.ToString(CultureInfo.InvariantCulture) + ", Received = " + received.ToString(CultureInfo.InvariantCulture) + ", Lost = " + lost.ToString(CultureInfo.InvariantCulture) + " (" + lossPercent.ToString(CultureInfo.InvariantCulture) + "% loss),");
            if (responseTimes.Count > 0)
            {
                Console.WriteLine("Approximate round trip times in milli-seconds:");
                Console.WriteLine("    Minimum = " + minimum.ToString(CultureInfo.InvariantCulture) + "ms, Maximum = " + maximum.ToString(CultureInfo.InvariantCulture) + "ms, Average = " + average.ToString(CultureInfo.InvariantCulture) + "ms");
            }

            return received > 0 ? 0 : 1;
        }

        private static async Task<int> HandleStopAsync(string[] args)
        {
            if (args.Length > 1 && IsHelp(args[1]))
            {
                PrintStopHelp();
                return 0;
            }

            bool force = false;
            for (int i = 1; i < args.Length; i++)
            {
                if (String.Equals(args[i], "--force", StringComparison.OrdinalIgnoreCase))
                {
                    force = true;
                    continue;
                }

                WriteError("Unknown stop option: " + args[i]);
                PrintStopHelp();
                return 1;
            }

            ClientStopResult result = await ClientWorkerController.StopAsync(force, DefaultStopTimeoutMs, CancellationToken.None).ConfigureAwait(false);
            if (!result.WasRunning)
            {
                if (_UseJson) WriteJson(new { stopped = true, reason = "not-running" });
                else WriteInfo("Xeno client is not running.");
                return 0;
            }

            int processId = result.ProcessId ?? 0;
            if (_UseJson)
            {
                WriteJson(new
                {
                    stopped = result.Stopped,
                    processId,
                    force
                });
                return result.Stopped ? 0 : 1;
            }

            if (result.Stopped)
            {
                WriteSuccess("Stopped Xeno client process " + processId + ".");
                return 0;
            }

            WriteWarning("Stop was requested, but process " + processId + " is still running. Use 'xeno stop --force' to terminate it.");
            return 1;
        }

        private static int HandleDashboard(string[] args, ClientSettings settings)
        {
            if (args.Length > 1 && IsHelp(args[1]))
            {
                PrintDashboardHelp();
                return 0;
            }

            bool open = args.Length > 1 && String.Equals(args[1], "open", StringComparison.OrdinalIgnoreCase);
            if (args.Length > 1 && !open)
            {
                WriteError("Unknown dashboard command: " + args[1]);
                PrintDashboardHelp();
                return 1;
            }

            ClientConnectionState state = ClientConnectionState.Load();
            if (!settings.LocalDashboard.Enabled && !state.LocalDashboardEnabled)
            {
                WriteInfo("Local dashboard is disabled. Enable LocalDashboard.Enabled in " + ClientSettings.SettingsFile + ".");
                return 1;
            }

            if (!state.LocalDashboardListening || String.IsNullOrWhiteSpace(state.LocalDashboardUrl))
            {
                WriteInfo("Local dashboard is enabled but not currently listening. Run 'xeno connect' or 'xeno start'.");
                if (!String.IsNullOrWhiteSpace(state.LocalDashboardLastError)) PrintStartField("Last error", state.LocalDashboardLastError);
                return 1;
            }

            WriteSuccess("Local dashboard: " + state.LocalDashboardUrl);
            if (open)
            {
                OpenBrowser(state.LocalDashboardUrl);
            }

            return 0;
        }

        private static int HandleReload(string[] args)
        {
            if (args.Length > 1 && IsHelp(args[1]))
            {
                PrintReloadHelp();
                return 0;
            }

            ClientSettings reloaded = ClientSettings.Load();
            string clientId = ServerEndpoints.EnsureClientId(reloaded);
            ClientConnectionState.MarkInventoryUpdateRequested();
            ClientConnectionState state = ClientConnectionState.Load();
            bool connected = state.IsConnected && state.IsRegistered && state.IsRecordedProcessRunning();

            if (_UseJson)
            {
                WriteJson(new
                {
                    reloaded = true,
                    settingsFile = ClientSettings.SettingsFile,
                    clientId,
                    connected,
                    inventoryUpdateRequested = true
                });
                return 0;
            }

            WriteSuccess("Reloaded " + ClientSettings.SettingsFile + ".");
            if (connected)
            {
                WriteInfo("A connected client worker will re-advertise the updated provider and model inventory on the next heartbeat.");
            }
            else
            {
                WriteInfo("No connected client worker is running. Updated inventory will be advertised on the next connection.");
            }

            return 0;
        }

        private static int HandleReset(string[] args)
        {
            bool force = false;
            for (int i = 1; i < args.Length; i++)
            {
                if (IsHelp(args[i]))
                {
                    PrintResetHelp();
                    return 0;
                }

                if (String.Equals(args[i], "--force", StringComparison.OrdinalIgnoreCase))
                {
                    force = true;
                    continue;
                }

                WriteError("Unknown reset option: " + args[i]);
                PrintResetHelp();
                return 1;
            }

            if (!force && !ConfirmReset())
            {
                if (_UseJson) WriteJson(new { reset = false, cancelled = true });
                else WriteInfo("Reset cancelled.");
                return 0;
            }

            ResetClientFiles();
            if (_UseJson)
            {
                WriteJson(new
                {
                    reset = true,
                    settingsFile = ClientSettings.SettingsFile,
                    stateFile = ClientConnectionState.StateFile
                });
            }
            else
            {
                WriteSuccessHeading("Reset Xeno client to factory defaults.");
                Console.WriteLine("  Settings: " + ClientSettings.SettingsFile);
                Console.WriteLine("  State:    " + ClientConnectionState.StateFile + " (removed)");
            }

            return 0;
        }

        private static bool ConfirmReset()
        {
            WriteWarningHeading("This will reset the Xeno client to factory defaults.");
            Console.WriteLine("  Settings: " + ClientSettings.SettingsFile);
            Console.WriteLine("  State:    " + ClientConnectionState.StateFile);
            Console.WriteLine();
            Console.Write("Type RESET to continue: ");
            string? confirmation = Console.ReadLine();
            return String.Equals(confirmation, "RESET", StringComparison.Ordinal);
        }

        private static void ResetClientFiles()
        {
            Directory.CreateDirectory(ClientSettings.SettingsDirectory);
            if (File.Exists(ClientSettings.SettingsFile)) File.Delete(ClientSettings.SettingsFile);
            if (File.Exists(ClientConnectionState.StateFile)) File.Delete(ClientConnectionState.StateFile);

            ClientSettings defaults = new ClientSettings();
            defaults.Runners.Add(new ClientRunnerSettings());
            defaults.Save();
        }

        private static int HandleConfig(string[] args, ClientSettings settings)
        {
            if (args.Length >= 2 && IsHelp(args[1]))
            {
                PrintConfigHelp();
                return 0;
            }

            if (args.Length >= 2 && args[1] == "show")
            {
                if (_UseJson) WriteJson(settings);
                else PrintConfig(settings);
                return 0;
            }

            if (args.Length >= 3 && args[1] == "colors")
            {
                if (String.Equals(args[2], "on", StringComparison.OrdinalIgnoreCase))
                {
                    settings.UseColors = true;
                    _UseColors = true;
                    settings.Save();
                    WriteSuccess("Colored output enabled.");
                    return 0;
                }

                if (String.Equals(args[2], "off", StringComparison.OrdinalIgnoreCase))
                {
                    settings.UseColors = false;
                    _UseColors = false;
                    settings.Save();
                    WriteSuccess("Colored output disabled.");
                    return 0;
                }
            }

            WriteError("Unknown config command.");
            PrintConfigHelp();
            return 1;
        }

        private static int HandleServer(string[] args, ClientSettings settings)
        {
            if (args.Length >= 2 && IsHelp(args[1]))
            {
                PrintServerHelp();
                return 0;
            }

            if (args.Length >= 3 && args[1] == "use")
            {
                ServerTarget target;
                try
                {
                    target = ServerTargets.Resolve(args[2]);
                }
                catch (ArgumentException ex)
                {
                    WriteError(ex.Message);
                    return 1;
                }

                settings.Server.Url = target.WebSocketUrl;
                settings.Server.RestUrl = target.RestUrl;
                settings.Save();
                WriteSuccess("Using " + target.Label + ": " + target.WebSocketUrl);
                if (ClientStatusSnapshot.Capture().Running) WriteWarning("Restart the client to connect to the new server: xeno restart");
                return 0;
            }

            if (args.Length == 2 && args[1] == "show")
            {
                ServerTarget current = ServerTargets.FromSettings(settings);
                Console.WriteLine("  Server:     " + DisplayFormat.OrDash(current.Label));
                Console.WriteLine("  WebSocket:  " + settings.Server.Url);
                Console.WriteLine("  REST:       " + DisplayFormat.OrDash(ServerEndpoints.TryBuildRestBaseUrl(settings)));
                return 0;
            }

            if (args.Length >= 3 && args[1] == "set-url")
            {
                settings.Server.Url = args[2];
                settings.Save();
                WriteSuccess("WebSocket server URL saved: " + settings.Server.Url);
                return 0;
            }

            if (args.Length >= 3 && args[1] == "set-rest-url")
            {
                settings.Server.RestUrl = args[2];
                settings.Save();
                WriteSuccess("REST server URL saved: " + settings.Server.RestUrl);
                return 0;
            }

            WriteError("Unknown server command.");
            PrintServerHelp();
            return 1;
        }

        private static int HandleAuth(string[] args, ClientSettings settings)
        {
            if (args.Length >= 2 && (args[1] == "--help" || args[1] == "-h" || args[1] == "help"))
            {
                PrintAuthHelp();
                return 0;
            }

            if (args.Length >= 3 && args[1] == "set-token")
            {
                settings.Server.Token = args[2];
                settings.Save();
                WriteWarning("Saved a REST token. WebSocket connections require xeno login or xeno set-auth --access-key.");
                return 0;
            }

            if (args.Length >= 3 && args[1] == "set-credential")
            {
                return HandleSetAuth(new[] { "set-auth", "--access-key", args[2] }, settings);
            }

            PrintAuthHelp();
            return 1;
        }

        private static int HandleSetAuth(string[] args, ClientSettings settings)
        {
            string? accessKey = null;
            bool clear = false;

            if (args.Length >= 2 && !args[1].StartsWith("-", StringComparison.Ordinal))
            {
                accessKey = args[1];
            }

            for (int i = 1; i < args.Length; i++)
            {
                if (args[i] == "--clear")
                {
                    clear = true;
                    continue;
                }

                if (args[i] == "--access-key" && i + 1 < args.Length)
                {
                    accessKey = args[++i];
                    continue;
                }

            }

            if (clear)
            {
                settings.Server.AccessKey = null;
                settings.Server.SecretKey = null;
                settings.Server.Token = null;
                settings.Save();
                WriteSuccess("Cleared stored server authentication.");
                return 0;
            }

            if (!String.IsNullOrWhiteSpace(accessKey))
            {
                settings.Server.AccessKey = accessKey;
                settings.Server.SecretKey = null;
                settings.Server.Token = null;
                settings.Save();
                WriteSuccess("Saved access key for server authentication.");
                return 0;
            }

            PrintAuthHelp();
            return 1;
        }

        private static async Task<int> HandleLoginAsync(string[] args, ClientSettings settings)
        {
            string username = String.Empty;
            string tenantId = settings.TenantId;
            string? serverUrl = null;

            for (int i = 1; i < args.Length; i++)
            {
                if ((args[i] == "-u" || args[i] == "--username") && i + 1 < args.Length)
                {
                    username = args[++i];
                    continue;
                }

                if (args[i] == "--tenant" && i + 1 < args.Length)
                {
                    tenantId = args[++i];
                    continue;
                }

                if (args[i] == "--server" && i + 1 < args.Length)
                {
                    serverUrl = args[++i];
                    continue;
                }
            }

            if (String.IsNullOrWhiteSpace(username))
            {
                WriteError("Username is required.");
                PrintLoginHelp();
                return 1;
            }

            try
            {
                if (ServerTargets.ApplySignInServer(settings, serverUrl))
                {
                    WriteInfo("Signing in to " + ServerTargets.FromSettings(settings).Label + " (" + settings.Server.Url + ").");
                }
            }
            catch (ArgumentException ex)
            {
                WriteError(ex.Message);
                return 1;
            }

            string password = ReadSecret("Password: ");
            if (String.IsNullOrWhiteSpace(password))
            {
                WriteError("Password is required.");
                return 1;
            }

            LoginResult login;
            using (XenoServerClient server = new XenoServerClient())
            {
                login = await server.LoginAsync(settings, username, password, tenantId, CancellationToken.None).ConfigureAwait(false);
            }

            if (!login.Success)
            {
                WriteError(login.StatusCode == null || login.Error == "Login response did not include a client credential." ? (login.Error ?? "Login failed.") : "Login failed: " + login.Error);
                return 1;
            }

            WriteSuccess("Login succeeded. Stored a tenant-bound client credential in " + ClientSettings.SettingsFile + ".");
            return 0;
        }

        private static async Task<int> HandleRunnerAsync(string[] args, ClientSettings settings)
        {
            if (args.Length >= 2 && IsHelp(args[1]))
            {
                PrintProviderHelp();
                return 0;
            }

            if (args.Length >= 2 && IsListCommand(args[1]))
            {
                PrintProviders(settings);
                return 0;
            }

            if (args.Length >= 2 && args[1] == "test")
            {
                string runnerName = String.Empty;
                for (int i = 2; i < args.Length - 1; i += 2)
                {
                    if (args[i] == "--name") runnerName = args[i + 1];
                }

                if (String.IsNullOrWhiteSpace(runnerName))
                {
                    WriteError("Provider endpoint name is required.");
                    PrintProviderHelp();
                    return 1;
                }

                ClientRunnerSettings? runner = ProviderConfiguration.FindProvider(settings, runnerName);
                if (runner == null)
                {
                    WriteError("Provider endpoint not found: " + runnerName);
                    return 1;
                }

                bool success = (await ProviderConfiguration.TestProviderAsync(runner, true, null, CancellationToken.None).ConfigureAwait(false)).Success;
                if (success) WriteSuccess("Provider endpoint reachable: " + runner.Name);
                else WriteError("Provider endpoint did not respond successfully: " + runner.Name);
                return success ? 0 : 2;
            }

            if (args.Length >= 2 && IsDeleteCommand(args[1]))
            {
                string runnerName = String.Empty;
                for (int i = 2; i < args.Length - 1; i += 2)
                {
                    if (args[i] == "--name") runnerName = args[i + 1];
                }

                if (String.IsNullOrWhiteSpace(runnerName))
                {
                    WriteError("Provider endpoint name is required.");
                    PrintProviderHelp();
                    return 1;
                }

                int removed = ProviderConfiguration.DeleteProvider(settings, runnerName);
                if (removed == 0)
                {
                    WriteError("Provider endpoint not found: " + runnerName);
                    return 1;
                }

                SaveInventorySettings(settings);
                if (removed == 1) WriteSuccess("Deleted provider endpoint '" + runnerName + "'.");
                else WriteSuccess("Deleted provider endpoint '" + runnerName + "' (" + removed + " entries removed).");
                return 0;
            }

            if (args.Length >= 2 && IsUpdateCommand(args[1]))
            {
                string runnerName = String.Empty;
                string? newName = null;
                string? apiType = null;
                string? endpoint = null;
                string? credential = null;
                bool clearCredential = false;
                bool? allowModelManagement = null;
                int? timeoutMs = null;
                bool? shareWithNetwork = null;
                bool? routeLocalRequests = null;
                int? maxConcurrent = null;
                bool inheritMaxConcurrent = false;
                for (int i = 2; i < args.Length; i++)
                {
                    string option = args[i];
                    if (String.Equals(option, "--clear-credential", StringComparison.OrdinalIgnoreCase))
                    {
                        clearCredential = true;
                        continue;
                    }

                    if (TryParseProviderRoutingFlag(args, ref i, out bool? parsedShare, out bool? parsedLocal))
                    {
                        if (parsedShare.HasValue) shareWithNetwork = parsedShare;
                        if (parsedLocal.HasValue) routeLocalRequests = parsedLocal;
                        continue;
                    }

                    if (String.Equals(option, "--max-concurrent", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!TryParseMaxConcurrentOption(args, ref i, out maxConcurrent, out inheritMaxConcurrent)) return 1;
                        continue;
                    }

                    if (String.Equals(option, "--allow-model-management", StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(option, "--model-management", StringComparison.OrdinalIgnoreCase))
                    {
                        if (i + 1 < args.Length && IsBooleanLiteral(args[i + 1])) allowModelManagement = ParseBooleanLiteral(args[++i]);
                        else allowModelManagement = true;
                        continue;
                    }

                    if (String.Equals(option, "--no-model-management", StringComparison.OrdinalIgnoreCase))
                    {
                        allowModelManagement = false;
                        continue;
                    }

                    if (i + 1 >= args.Length)
                    {
                        WriteError(option + " requires a value.");
                        PrintProviderHelp();
                        return 1;
                    }

                    if (String.Equals(option, "--name", StringComparison.OrdinalIgnoreCase)) runnerName = args[++i];
                    else if (String.Equals(option, "--new-name", StringComparison.OrdinalIgnoreCase)) newName = args[++i];
                    else if (String.Equals(option, "--type", StringComparison.OrdinalIgnoreCase) || String.Equals(option, "--api-type", StringComparison.OrdinalIgnoreCase)) apiType = args[++i];
                    else if (String.Equals(option, "--endpoint", StringComparison.OrdinalIgnoreCase)) endpoint = args[++i];
                    else if (String.Equals(option, "--credential", StringComparison.OrdinalIgnoreCase)) credential = args[++i];
                    else if (String.Equals(option, "--timeout", StringComparison.OrdinalIgnoreCase) || String.Equals(option, "--timeout-ms", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!Int32.TryParse(args[++i], out int parsedTimeout) || parsedTimeout < 1000)
                        {
                            WriteError(option + " requires a timeout in milliseconds greater than or equal to 1000.");
                            return 1;
                        }

                        timeoutMs = parsedTimeout;
                    }
                    else
                    {
                        WriteError("Unknown provider update option: " + option);
                        PrintProviderHelp();
                        return 1;
                    }
                }

                if (String.IsNullOrWhiteSpace(runnerName))
                {
                    WriteError("Provider endpoint name is required.");
                    PrintProviderHelp();
                    return 1;
                }

                ClientRunnerSettings runner;
                try
                {
                    runner = ProviderConfiguration.UpdateProvider(settings, runnerName, new ProviderUpdateRequest
                    {
                        NewName = newName,
                        ApiType = apiType,
                        Endpoint = endpoint,
                        Credential = credential,
                        ClearCredential = clearCredential,
                        AllowModelManagement = allowModelManagement,
                        TimeoutMs = timeoutMs,
                        ShareWithNetwork = shareWithNetwork,
                        RouteLocalRequests = routeLocalRequests,
                        MaxConcurrentRequests = maxConcurrent,
                        InheritMaxConcurrentRequests = inheritMaxConcurrent
                    });
                }
                catch (Exception ex) when (ex is KeyNotFoundException || ex is ArgumentException)
                {
                    WriteError(ex.Message);
                    return 1;
                }

                SaveInventorySettings(settings);
                WriteSuccess("Updated provider endpoint '" + runner.Name + "'.");
                return 0;
            }

            if (args.Length >= 2 && args[1] == "add")
            {
                ClientRunnerSettings runner = new ClientRunnerSettings();
                runner.Name = String.Empty;
                runner.ApiType = String.Empty;
                runner.Endpoint = String.Empty;
                bool usedLegacyProviderOption = false;
                for (int i = 2; i < args.Length; i++)
                {
                    string option = args[i];
                    if (TryParseProviderRoutingFlag(args, ref i, out bool? parsedShare, out bool? parsedLocal))
                    {
                        if (parsedShare.HasValue) runner.ShareWithNetwork = parsedShare.Value;
                        if (parsedLocal.HasValue) runner.RouteLocalRequests = parsedLocal.Value;
                        continue;
                    }

                    if (String.Equals(option, "--max-concurrent", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!TryParseMaxConcurrentOption(args, ref i, out int? addLimit, out bool _)) return 1;
                        runner.MaxConcurrentRequests = addLimit;
                        continue;
                    }
                    if (String.Equals(option, "--allow-model-management", StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(option, "--model-management", StringComparison.OrdinalIgnoreCase))
                    {
                        if (i + 1 < args.Length && IsBooleanLiteral(args[i + 1])) runner.AllowModelManagement = ParseBooleanLiteral(args[++i]);
                        else runner.AllowModelManagement = true;
                        continue;
                    }

                    if (String.Equals(option, "--no-model-management", StringComparison.OrdinalIgnoreCase))
                    {
                        runner.AllowModelManagement = false;
                        continue;
                    }

                    if (i + 1 >= args.Length)
                    {
                        WriteError(option + " requires a value.");
                        PrintProviderHelp();
                        return 1;
                    }

                    if (option == "--name") runner.Name = args[++i];
                    else if (option == "--type" || option == "--api-type") runner.ApiType = args[++i];
                    else if (option == "--provider")
                    {
                        runner.ApiType = args[++i];
                        usedLegacyProviderOption = true;
                    }
                    else if (option == "--endpoint") runner.Endpoint = args[++i];
                    else if (option == "--credential") runner.Credential = args[++i];
                    else if (option == "--timeout" || option == "--timeout-ms")
                    {
                        if (!Int32.TryParse(args[++i], out int parsedTimeout) || parsedTimeout < 1000)
                        {
                            WriteError(option + " requires a timeout in milliseconds greater than or equal to 1000.");
                            return 1;
                        }

                        runner.TimeoutMs = parsedTimeout;
                    }
                    else
                    {
                        WriteError("Unknown provider add option: " + option);
                        PrintProviderHelp();
                        return 1;
                    }
                }

                if (String.IsNullOrWhiteSpace(runner.Name) || String.IsNullOrWhiteSpace(runner.ApiType) || String.IsNullOrWhiteSpace(runner.Endpoint))
                {
                    List<string> missing = new List<string>();
                    if (String.IsNullOrWhiteSpace(runner.Name)) missing.Add("--name");
                    if (String.IsNullOrWhiteSpace(runner.ApiType)) missing.Add("--type");
                    if (String.IsNullOrWhiteSpace(runner.Endpoint)) missing.Add("--endpoint");
                    WriteError("Missing required option(s): " + String.Join(", ", missing) + ".");
                    PrintProviderHelp();
                    return 1;
                }

                if (runner.AllowModelManagement && !ProviderConfiguration.IsOllamaApiType(runner.ApiType))
                {
                    WriteError("Model management can only be enabled for Ollama providers.");
                    return 1;
                }

                if (usedLegacyProviderOption) WriteWarning("--provider is deprecated for 'xeno provider add'; use --type for the upstream API type.");

                ProviderAddResult addResult = ProviderConfiguration.AddProvider(settings, runner);
                if (!addResult.Added)
                {
                    if (addResult.NameConflict)
                    {
                        WriteInfo("Provider endpoint '" + addResult.Provider.Name + "' already exists; no changes made.");
                    }
                    else
                    {
                        WriteInfo("Provider endpoint '" + addResult.Provider.Name + "' already uses " + addResult.Provider.Endpoint + "; no changes made.");
                    }

                    return 0;
                }

                SaveInventorySettings(settings);
                WriteSuccess("Added provider endpoint '" + runner.Name + "'.");
                return 0;
            }

            WriteError("Unknown provider command.");
            PrintProviderHelp();
            return 1;
        }

        private static async Task<int> HandleInferenceAsync(string[] args, ClientSettings settings)
        {
            string sub = args.Length >= 2 ? args[1].ToLowerInvariant() : "routes";
            LocalInferenceRuntimeStatus? runtime = LocalInferenceRuntimeStatus.Load();
            if (sub == "routes" || sub == "rules")
            {
                LocalInferenceRoutingView view = LocalInferenceRoutingService.BuildView(settings, runtime);
                List<object> rows = new List<object>();
                foreach (LocalInferenceRouteRule rule in view.Rules)
                {
                    LocalInferenceRouteDecision now = LocalInferenceRoutingService.Resolve(settings, runtime, rule.Model);
                    rows.Add(new { model = rule.Model, target = rule.Target.ToString(), provider = rule.Provider, fallbackToNetwork = rule.FallbackToNetwork, routesTo = now.RouteHeader });
                }

                if (_UseJson)
                {
                    WriteJson(new { routingMode = view.RoutingMode, rules = rows });
                    return 0;
                }

                WriteInfoHeading("Local inference routing: " + view.RoutingMode);
                if (view.Rules.Count == 0)
                {
                    Console.WriteLine("  No route rules. Models served on this machine are answered here; everything else goes to the Xeno network.");
                    Console.WriteLine();
                    return 0;
                }

                foreach (LocalInferenceRouteRule rule in view.Rules)
                {
                    LocalInferenceRouteDecision now = LocalInferenceRoutingService.Resolve(settings, runtime, rule.Model);
                    string target = rule.Target == LocalInferenceRouteTargetEnum.Network ? "Xeno network" : (rule.Provider ?? String.Empty);
                    Console.WriteLine("  - " + rule.Model + " -> " + target + (rule.FallbackToNetwork ? " (falls back to the network)" : String.Empty) + "; now goes to " + now.RouteHeader);
                }

                Console.WriteLine();
                return 0;
            }

            if (sub == "resolve")
            {
                string model = args.Length >= 3 ? args[2] : String.Empty;
                if (String.IsNullOrWhiteSpace(model))
                {
                    WriteError("Usage: xeno inference resolve <model> [--ollama]");
                    return 1;
                }

                LocalInferenceApiShapeEnum shape = args.Any(item => String.Equals(item, "--ollama", StringComparison.OrdinalIgnoreCase)) ? LocalInferenceApiShapeEnum.Ollama : LocalInferenceApiShapeEnum.OpenAi;
                LocalInferenceRouteDecision decision = LocalInferenceRoutingService.Resolve(settings, runtime, model, shape);
                if (_UseJson)
                {
                    WriteJson(LocalInferenceRoutingService.ToResponse(decision));
                    return 0;
                }

                WriteInfoHeading(model + " goes to " + decision.RouteHeader);
                Console.WriteLine("  " + LocalInferenceRoutingService.DescribeReason(decision.Reason));
                if (decision.FallbackToNetwork) Console.WriteLine("  If the provider fails before responding, the request falls back to the Xeno network.");
                if (decision.Target == LocalInferenceRouteTargetKindEnum.Local && !decision.ShapeSupported) Console.WriteLine("  This provider accepts OpenAI-compatible requests only; call the /v1 routes.");
                if (runtime == null) Console.WriteLine("  The client is not running, so provider health is unknown.");
                Console.WriteLine();
                return 0;
            }

            if (sub == "route")
            {
                string action = args.Length >= 3 ? args[2].ToLowerInvariant() : String.Empty;
                string model = args.Length >= 4 ? args[3] : String.Empty;
                if ((action != "set" && action != "remove" && action != "rm" && action != "delete") || String.IsNullOrWhiteSpace(model))
                {
                    PrintInferenceHelp();
                    return 1;
                }

                if (action != "set")
                {
                    int removed = LocalInferenceRoutingService.DeleteRule(settings, model);
                    if (removed == 0)
                    {
                        WriteError("No route rule for '" + model + "'.");
                        return 1;
                    }

                    settings.Save();
                    WriteSuccess("Removed the route rule for '" + model + "'.");
                    return 0;
                }

                string? provider = null;
                bool network = false;
                bool fallback = false;
                for (int i = 4; i < args.Length; i++)
                {
                    if (String.Equals(args[i], "--network", StringComparison.OrdinalIgnoreCase)) network = true;
                    else if (String.Equals(args[i], "--fallback", StringComparison.OrdinalIgnoreCase)) fallback = true;
                    else if (String.Equals(args[i], "--local", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) provider = args[++i];
                    else
                    {
                        WriteError("Unknown route option: " + args[i]);
                        PrintInferenceHelp();
                        return 1;
                    }
                }

                if (network == (provider != null))
                {
                    WriteError("Give exactly one of --local <provider> or --network.");
                    return 1;
                }

                try
                {
                    LocalInferenceRoutingService.SaveRule(settings, new LocalInferenceRouteRule
                    {
                        Model = model,
                        Target = network ? LocalInferenceRouteTargetEnum.Network : LocalInferenceRouteTargetEnum.Local,
                        Provider = provider,
                        FallbackToNetwork = fallback && !network
                    });
                }
                catch (ArgumentException ex)
                {
                    WriteError(ex.Message);
                    return 1;
                }

                settings.Save();
                WriteSuccess("Saved the route rule for '" + model + "'. It applies to new requests right away.");
                return 0;
            }

            if (sub == "backends" || sub == "providers")
            {
                bool check = args.Any(item => String.Equals(item, "--check", StringComparison.OrdinalIgnoreCase));
                LocalInferenceRoutingView view = LocalInferenceRoutingService.BuildView(settings, runtime);
                if (check)
                {
                    IReadOnlyCollection<LocalBackendStatus> statuses = await LocalInferenceRoutingService.CheckBackendsAsync(settings, CancellationToken.None).ConfigureAwait(false);
                    view = LocalInferenceRoutingService.BuildView(settings, new LocalInferenceRuntimeStatus { Backends = statuses.ToList(), Concurrency = runtime?.Concurrency ?? new List<ProviderConcurrencySnapshot>() });
                }

                if (_UseJson)
                {
                    WriteJson(view.Backends);
                    return 0;
                }

                WriteInfoHeading("Local providers" + (check ? " (checked now)" : runtime == null ? " (client not running; health unknown)" : String.Empty));
                foreach (LocalBackendView backend in view.Backends)
                {
                    LocalBackendRow row = LocalBackendRow.From(backend);
                    Console.WriteLine("  - " + row.Provider + " (" + row.ApiType + ")");
                    Console.WriteLine("    Health:    " + row.Health + (backend.LatencyMs.HasValue ? ", " + row.Latency : String.Empty));
                    Console.WriteLine("    In use:    " + row.Load);
                    Console.WriteLine("    Shared:    " + row.Shared);
                    if (!String.IsNullOrWhiteSpace(backend.LastError)) Console.WriteLine("    Error:     " + backend.LastError);
                }

                Console.WriteLine();
                return 0;
            }

            if (sub == "concurrency")
            {
                if (args.Length < 3 || !Int32.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int limit) || limit < 1 || limit > 256)
                {
                    WriteError("Usage: xeno inference concurrency <1-256>");
                    return 1;
                }

                settings.DefaultMaxConcurrentRequests = limit;
                settings.Save();
                WriteSuccess("Default max concurrent requests per provider is now " + limit.ToString(CultureInfo.InvariantCulture) + ".");
                return 0;
            }

            if (sub == "requests")
            {
                int take = 20;
                for (int i = 2; i < args.Length - 1; i++)
                {
                    if (String.Equals(args[i], "--limit", StringComparison.OrdinalIgnoreCase) && Int32.TryParse(args[i + 1], out int parsed)) take = Math.Min(1000, Math.Max(1, parsed));
                }

                List<LocalInferenceRequestRecord> records = LocalInferenceRequestLog.Read(0, take);
                if (_UseJson)
                {
                    WriteJson(records);
                    return 0;
                }

                WriteInfoHeading("Recent local endpoint requests");
                if (records.Count == 0) Console.WriteLine("  None yet.");
                foreach (LocalInferenceRequestRecord record in records)
                {
                    LocalInferenceRequestRow row = LocalInferenceRequestRow.From(record);
                    Console.WriteLine("  " + row.When + "  " + row.Status + "  " + row.Latency.PadLeft(8) + "  " + row.Route.PadRight(24) + "  " + row.Model);
                }

                Console.WriteLine();
                return 0;
            }

            PrintInferenceHelp();
            return 1;
        }

        private static void PrintInferenceHelp()
        {
            WriteInfoHeading("Local inference commands");
            Console.WriteLine("  xeno inference routes");
            Console.WriteLine("      List route rules and where each model goes now.");
            Console.WriteLine("  xeno inference resolve <model> [--ollama]");
            Console.WriteLine("      Show where the local endpoint would send a model, and why.");
            Console.WriteLine("  xeno inference route set <model> --local <provider> [--fallback]");
            Console.WriteLine("  xeno inference route set <model> --network");
            Console.WriteLine("      Pin a model to a provider on this machine or to the Xeno network. --fallback lets a local request");
            Console.WriteLine("      go to the network if the provider fails before responding.");
            Console.WriteLine("  xeno inference route remove <model>");
            Console.WriteLine("  xeno inference backends [--check]");
            Console.WriteLine("      Provider health and load. --check probes every provider now.");
            Console.WriteLine("  xeno inference concurrency <1-256>");
            Console.WriteLine("      Set the default max concurrent requests per provider.");
            Console.WriteLine("  xeno inference requests [--limit <n>]");
            Console.WriteLine("      Recent requests apps on this machine sent to the local endpoint (metadata only).");
            Console.WriteLine();
        }

        private static int HandleModel(string[] args, ClientSettings settings)
        {
            if (args.Length >= 2 && IsHelp(args[1]))
            {
                PrintModelHelp();
                return 0;
            }

            if (args.Length >= 2 && IsListCommand(args[1]))
            {
                PrintModels(settings);
                return 0;
            }

            if (args.Length >= 2 && args[1] == "add")
            {
                string runnerName = String.Empty;
                string modelName = String.Empty;
                bool supportsCompletions = true;
                bool supportsEmbeddings = false;
                int? addModelLimit = null;
                for (int i = 2; i < args.Length; i++)
                {
                    string option = args[i];
                    if (String.Equals(option, "--max-concurrent", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!TryParseMaxConcurrentOption(args, ref i, out addModelLimit, out bool _)) return 1;
                        continue;
                    }

                    if (String.Equals(option, "--runner", StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(option, "--provider", StringComparison.OrdinalIgnoreCase))
                    {
                        if (i + 1 >= args.Length)
                        {
                            WriteError(option + " requires a value.");
                            PrintModelHelp();
                            return 1;
                        }

                        runnerName = args[++i];
                        continue;
                    }

                    if (String.Equals(option, "--name", StringComparison.OrdinalIgnoreCase))
                    {
                        if (i + 1 >= args.Length)
                        {
                            WriteError("--name requires a value.");
                            PrintModelHelp();
                            return 1;
                        }

                        modelName = args[++i];
                        continue;
                    }

                    if (String.Equals(option, "--completion", StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(option, "--completions", StringComparison.OrdinalIgnoreCase))
                    {
                        if (i + 1 < args.Length && IsBooleanLiteral(args[i + 1]))
                        {
                            supportsCompletions = ParseBooleanLiteral(args[++i]);
                        }
                        else
                        {
                            supportsCompletions = true;
                        }

                        continue;
                    }

                    if (String.Equals(option, "--embedding", StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(option, "--embeddings", StringComparison.OrdinalIgnoreCase))
                    {
                        if (i + 1 < args.Length && IsBooleanLiteral(args[i + 1]))
                        {
                            supportsEmbeddings = ParseBooleanLiteral(args[++i]);
                        }
                        else
                        {
                            supportsEmbeddings = true;
                        }

                        continue;
                    }

                    if (String.Equals(option, "--no-completion", StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(option, "--no-completions", StringComparison.OrdinalIgnoreCase))
                    {
                        supportsCompletions = false;
                        continue;
                    }

                    if (String.Equals(option, "--no-embedding", StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(option, "--no-embeddings", StringComparison.OrdinalIgnoreCase))
                    {
                        supportsEmbeddings = false;
                        continue;
                    }

                    if (String.Equals(option, "--type", StringComparison.OrdinalIgnoreCase))
                    {
                        if (i + 1 >= args.Length)
                        {
                            WriteError("--type requires completion, embedding, or both.");
                            PrintModelHelp();
                            return 1;
                        }

                        string type = args[++i];
                        if (String.Equals(type, "completion", StringComparison.OrdinalIgnoreCase) ||
                            String.Equals(type, "completions", StringComparison.OrdinalIgnoreCase))
                        {
                            supportsCompletions = true;
                            supportsEmbeddings = false;
                            continue;
                        }

                        if (String.Equals(type, "embedding", StringComparison.OrdinalIgnoreCase) ||
                            String.Equals(type, "embeddings", StringComparison.OrdinalIgnoreCase))
                        {
                            supportsCompletions = false;
                            supportsEmbeddings = true;
                            continue;
                        }

                        if (String.Equals(type, "both", StringComparison.OrdinalIgnoreCase) ||
                            String.Equals(type, "all", StringComparison.OrdinalIgnoreCase))
                        {
                            supportsCompletions = true;
                            supportsEmbeddings = true;
                            continue;
                        }

                        WriteError("--type requires completion, embedding, or both.");
                        PrintModelHelp();
                        return 1;
                    }

                    WriteError("Unknown model add option: " + option);
                    PrintModelHelp();
                    return 1;
                }

                if (String.IsNullOrWhiteSpace(runnerName) || String.IsNullOrWhiteSpace(modelName))
                {
                    List<string> missing = new List<string>();
                    if (String.IsNullOrWhiteSpace(runnerName)) missing.Add("--provider");
                    if (String.IsNullOrWhiteSpace(modelName)) missing.Add("--name");
                    WriteError("Missing required option(s): " + String.Join(", ", missing) + ".");
                    PrintModelHelp();
                    return 1;
                }

                ClientRunnerSettings runner;
                try
                {
                    runner = ProviderConfiguration.AddModel(settings, runnerName, modelName, supportsCompletions, supportsEmbeddings);
                    if (addModelLimit.HasValue)
                    {
                        ProviderConfiguration.UpdateModel(settings, new ModelUpdateRequest { Provider = runner.Name, Name = modelName, MaxConcurrentRequests = addModelLimit });
                    }
                }
                catch (KeyNotFoundException ex)
                {
                    WriteError(ex.Message);
                    return 1;
                }
                catch (ArgumentException ex)
                {
                    WriteError(ex.Message);
                    PrintModelHelp();
                    return 1;
                }

                SaveInventorySettings(settings);
                WriteSuccess("Added model '" + modelName + "' to provider endpoint '" + runner.Name + "'.");
                return 0;
            }

            if (args.Length >= 2 && IsUpdateCommand(args[1]))
            {
                string runnerName = String.Empty;
                string modelName = String.Empty;
                string? newRunnerName = null;
                string? newModelName = null;
                bool? supportsCompletions = null;
                bool? supportsEmbeddings = null;
                int? updateModelLimit = null;
                bool inheritModelLimit = false;
                for (int i = 2; i < args.Length; i++)
                {
                    string option = args[i];
                    if (String.Equals(option, "--max-concurrent", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!TryParseMaxConcurrentOption(args, ref i, out updateModelLimit, out inheritModelLimit)) return 1;
                        continue;
                    }

                    if (String.Equals(option, "--completion", StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(option, "--completions", StringComparison.OrdinalIgnoreCase))
                    {
                        supportsCompletions = true;
                        if (i + 1 < args.Length && IsBooleanLiteral(args[i + 1])) supportsCompletions = ParseBooleanLiteral(args[++i]);
                        continue;
                    }

                    if (String.Equals(option, "--embedding", StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(option, "--embeddings", StringComparison.OrdinalIgnoreCase))
                    {
                        supportsEmbeddings = true;
                        if (i + 1 < args.Length && IsBooleanLiteral(args[i + 1])) supportsEmbeddings = ParseBooleanLiteral(args[++i]);
                        continue;
                    }

                    if (String.Equals(option, "--no-completion", StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(option, "--no-completions", StringComparison.OrdinalIgnoreCase))
                    {
                        supportsCompletions = false;
                        continue;
                    }

                    if (String.Equals(option, "--no-embedding", StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(option, "--no-embeddings", StringComparison.OrdinalIgnoreCase))
                    {
                        supportsEmbeddings = false;
                        continue;
                    }

                    if (i + 1 >= args.Length)
                    {
                        WriteError(option + " requires a value.");
                        PrintModelHelp();
                        return 1;
                    }

                    if (String.Equals(option, "--runner", StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(option, "--provider", StringComparison.OrdinalIgnoreCase))
                    {
                        runnerName = args[++i];
                        continue;
                    }

                    if (String.Equals(option, "--name", StringComparison.OrdinalIgnoreCase))
                    {
                        modelName = args[++i];
                        continue;
                    }

                    if (String.Equals(option, "--new-provider", StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(option, "--new-runner", StringComparison.OrdinalIgnoreCase))
                    {
                        newRunnerName = args[++i];
                        continue;
                    }

                    if (String.Equals(option, "--new-name", StringComparison.OrdinalIgnoreCase))
                    {
                        newModelName = args[++i];
                        continue;
                    }

                    if (String.Equals(option, "--type", StringComparison.OrdinalIgnoreCase))
                    {
                        string type = args[++i];
                        if (String.Equals(type, "completion", StringComparison.OrdinalIgnoreCase) ||
                            String.Equals(type, "completions", StringComparison.OrdinalIgnoreCase))
                        {
                            supportsCompletions = true;
                            supportsEmbeddings = false;
                            continue;
                        }

                        if (String.Equals(type, "embedding", StringComparison.OrdinalIgnoreCase) ||
                            String.Equals(type, "embeddings", StringComparison.OrdinalIgnoreCase))
                        {
                            supportsCompletions = false;
                            supportsEmbeddings = true;
                            continue;
                        }

                        if (String.Equals(type, "both", StringComparison.OrdinalIgnoreCase) ||
                            String.Equals(type, "all", StringComparison.OrdinalIgnoreCase))
                        {
                            supportsCompletions = true;
                            supportsEmbeddings = true;
                            continue;
                        }

                        WriteError("--type requires completion, embedding, or both.");
                        PrintModelHelp();
                        return 1;
                    }

                    WriteError("Unknown model update option: " + option);
                    PrintModelHelp();
                    return 1;
                }

                if (String.IsNullOrWhiteSpace(modelName))
                {
                    WriteError("Model name is required.");
                    PrintModelHelp();
                    return 1;
                }

                ModelUpdateResult updated;
                try
                {
                    updated = ProviderConfiguration.UpdateModel(settings, new ModelUpdateRequest
                    {
                        Provider = runnerName,
                        Name = modelName,
                        NewProvider = newRunnerName,
                        NewName = newModelName,
                        SupportsCompletions = supportsCompletions,
                        SupportsEmbeddings = supportsEmbeddings,
                        MaxConcurrentRequests = updateModelLimit,
                        InheritMaxConcurrentRequests = inheritModelLimit
                    });
                }
                catch (KeyNotFoundException ex)
                {
                    WriteError(ex.Message);
                    return 1;
                }
                catch (ArgumentException ex)
                {
                    WriteError(ex.Message);
                    if (ex.Message.StartsWith("Model must support", StringComparison.Ordinal)) PrintModelHelp();
                    return 1;
                }

                SaveInventorySettings(settings);
                WriteSuccess("Updated model '" + updated.Model.Name + "' in provider endpoint '" + updated.Provider.Name + "'.");
                return 0;
            }

            if (args.Length >= 2 && IsDeleteCommand(args[1]))
            {
                string runnerName = String.Empty;
                string modelName = String.Empty;
                for (int i = 2; i < args.Length - 1; i += 2)
                {
                    if (args[i] == "--runner" || args[i] == "--provider") runnerName = args[i + 1];
                    if (args[i] == "--name") modelName = args[i + 1];
                }

                if (String.IsNullOrWhiteSpace(modelName))
                {
                    WriteError("Model name is required.");
                    PrintModelHelp();
                    return 1;
                }

                int removed;
                try
                {
                    removed = ProviderConfiguration.DeleteModel(settings, runnerName, modelName);
                }
                catch (KeyNotFoundException ex)
                {
                    WriteError(ex.Message);
                    return 1;
                }

                if (removed == 0)
                {
                    if (String.IsNullOrWhiteSpace(runnerName)) WriteError("Model not found: " + modelName);
                    else WriteError("Model not found in provider endpoint '" + runnerName + "': " + modelName);
                    return 1;
                }

                SaveInventorySettings(settings);
                if (!String.IsNullOrWhiteSpace(runnerName)) WriteSuccess("Deleted model '" + modelName + "' from provider endpoint '" + runnerName + "'.");
                else if (removed == 1) WriteSuccess("Deleted model '" + modelName + "'.");
                else WriteSuccess("Deleted model '" + modelName + "' (" + removed + " entries removed).");
                return 0;
            }

            WriteError("Unknown model command.");
            PrintModelHelp();
            return 1;
        }

        private static async Task<int> StartBackgroundConnectAsync(ClientSettings settings, int timeoutMs, CancellationToken token)
        {
            settings.ValidateLocalInferenceProviderEndpoints();
            ServerEndpoints.EnsureClientId(settings);
            if (!ServerEndpoints.HasWebSocketCredential(settings))
            {
                throw new InvalidOperationException("Run 'xeno login -u <username>' or configure an access key before connecting.");
            }

            ClientConnectionState existing = ClientConnectionState.Load();
            if (!ClientWorkerController.IsHealthyWorkerRunning(settings, existing)) WriteConnectionAttempt(settings);
            ClientStartResult result = await ClientWorkerController.StartAsync(settings, XenoExecutableLocator.CurrentProcess(typeof(Program).Assembly), timeoutMs, token).ConfigureAwait(false);
            ClientConnectionState state = result.State;
            if (result.Outcome == ClientStartOutcomeEnum.AlreadyRunning)
            {
                if (_UseJson)
                {
                    WriteJson(new
                    {
                        connected = true,
                        alreadyRunning = true,
                        websocketUrl = settings.Server.Url,
                        restUrl = ServerEndpoints.BuildRestBaseUrl(settings),
                        processId = result.ProcessId,
                        logFile = state.LogFilePath ?? ClientConnectionState.LogFile,
                        localDashboard = BuildLocalDashboardJson(state),
                        localInferenceServer = BuildLocalInferenceJson(state, settings)
                    });
                }
                else
                {
                    WriteInfo("Xeno client is already connected.");
                    PrintServerAccess(settings);
                    PrintStartField("Process ID", result.ProcessId == 0 ? "(none)" : result.ProcessId.ToString(CultureInfo.InvariantCulture));
                    PrintStartField("Log file", DisplayValue(state.LogFilePath, ClientConnectionState.LogFile));
                    PrintLocalDashboardAccess(state);
                    PrintLocalInferenceAccess(state, settings);
                }

                return 0;
            }

            bool registered = result.Outcome == ClientStartOutcomeEnum.Started;
            if (_UseJson)
            {
                WriteJson(new
                {
                    connected = registered,
                    websocketUrl = settings.Server.Url,
                    restUrl = ServerEndpoints.BuildRestBaseUrl(settings),
                    processId = result.ProcessId,
                    logFile = state.LogFilePath ?? ClientConnectionState.LogFile,
                    errorLogFile = state.ErrorLogFilePath ?? ClientConnectionState.ErrorLogFile,
                    reason = state.LastDisconnectReason ?? String.Empty,
                    localDashboard = BuildLocalDashboardJson(state),
                    localInferenceServer = BuildLocalInferenceJson(state, settings)
                });
            }
            else if (registered)
            {
                WriteSuccess("Xeno client connected in the background.");
                PrintServerAccess(settings);
                PrintStartField("Process ID", result.ProcessId.ToString(CultureInfo.InvariantCulture));
                PrintStartField("Log file", DisplayValue(state.LogFilePath, ClientConnectionState.LogFile));
                PrintLocalDashboardAccess(state);
                PrintLocalInferenceAccess(state, settings);
            }
            else
            {
                WriteError("Xeno client did not register within " + timeoutMs + "ms.");
                PrintServerAccess(settings);
                PrintStartField("Process ID", result.ProcessId.ToString(CultureInfo.InvariantCulture));
                PrintStartField("Log file", DisplayValue(state.LogFilePath, ClientConnectionState.LogFile));
                PrintLocalDashboardAccess(state);
                PrintLocalInferenceAccess(state, settings);
                PrintStartField("Last reason", DisplayValue(state.LastDisconnectReason, "(none)"));
            }

            return registered ? 0 : 1;
        }

        private static async Task RunForegroundConnectAsync(ClientSettings settings, bool worker, CancellationToken token)
        {
            settings.ValidateLocalInferenceProviderEndpoints();
            ClientConnectionState.ClearStopRequest();
            using (CancellationTokenSource runtimeTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                ConsoleCancelEventHandler? cancelHandler = delegate(object? sender, ConsoleCancelEventArgs args)
                {
                    args.Cancel = true;
                    runtimeTokenSource.Cancel();
                };
                Console.CancelKeyPress += cancelHandler;
                Task stopMonitor = MonitorStopFileAsync(runtimeTokenSource, runtimeTokenSource.Token);
                try
                {
                    ClientConnectionState.MarkStarted(settings, worker);
                    await LocalDashboardRuntime.StartAsync(settings, runtimeTokenSource.Token).ConfigureAwait(false);
                    await LocalInferenceServerRuntime.StartAsync(settings, runtimeTokenSource.Token).ConfigureAwait(false);
                    await Xeno.Client.Mcp.XenoMcpHttpRuntime.StartAsync(settings, LocalDashboardRuntime.Tools, runtimeTokenSource.Token).ConfigureAwait(false);
                    await ConnectAsync(settings, runtimeTokenSource.Token).ConfigureAwait(false);
                }
                finally
                {
                    Xeno.Client.Mcp.XenoMcpHttpRuntime.Stop();
                    LocalInferenceServerRuntime.Stop(settings);
                    LocalDashboardRuntime.Stop();
                    ClientConnectionState.MarkLocalDashboardStopped(settings);
                    runtimeTokenSource.Cancel();
                    try
                    {
                        await stopMonitor.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                    }

                    Console.CancelKeyPress -= cancelHandler;
                    ClientConnectionState.ClearStopRequest();
                }
            }
        }

        private static void WriteConnectionAttempt(ClientSettings settings)
        {
            if (_UseJson) return;
            WriteInfo("Attempting connection to server at " + settings.Server.Url);
        }

        private static async Task ConnectAsync(ClientSettings settings, CancellationToken token)
        {
            ServerEndpoints.EnsureClientId(settings);
            if (!ServerEndpoints.HasWebSocketCredential(settings))
            {
                throw new InvalidOperationException("Run 'xeno login -u <username>' or configure an access key before connecting.");
            }

            WriteConnectionAttempt(settings);
            int reconnectDelayMs = settings.Server.ReconnectInitialDelayMs;
            while (!token.IsCancellationRequested)
            {
                ClientSettings connectionSettings = settings;
                try
                {
                    connectionSettings = LoadRuntimeSettingsForConnectedSession(settings);
                    using (ClientWebSocket socket = new ClientWebSocket())
                    {
                        ApplyServerAuthentication(socket, connectionSettings);
                        await socket.ConnectAsync(new Uri(connectionSettings.Server.Url), token).ConfigureAwait(false);

                        // Reload settings after the connect completes so the advertised inventory reflects
                        // the latest on-disk configuration. ConnectAsync can block while the server is
                        // starting (for example across a restart), during which the operator may update
                        // provider settings; loading only before the connect would register stale inventory.
                        connectionSettings = LoadRuntimeSettingsForConnectedSession(settings);
                        ClientConnectionState.MarkWebSocketConnected(connectionSettings);
                        await SendMessageAsync(socket, new { type = "client.register", clientId = connectionSettings.ClientId, tenantId = connectionSettings.TenantId, name = connectionSettings.Name, inventory = connectionSettings.BuildInventoryMessage() }, token).ConfigureAwait(false);
                        string? disconnectReason = await ReceiveLoopAsync(socket, connectionSettings, token).ConfigureAwait(false);
                        ClientConnectionState.MarkDisconnected(connectionSettings, disconnectReason ?? "WebSocket closed.");
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    ClientConnectionState.MarkDisconnected(connectionSettings, "Client stopped.");
                    return;
                }
                catch (Exception ex)
                {
                    ClientConnectionState.MarkDisconnected(connectionSettings, ex.Message);
                    WriteWarning("Disconnected from Xeno server: " + ex.Message);
                }

                if (ClientConnectionState.IsStopRequested())
                {
                    ClientConnectionState.MarkDisconnected(connectionSettings, "Client stopped.");
                    return;
                }

                await DelayBeforeReconnectAsync(reconnectDelayMs, connectionSettings, token).ConfigureAwait(false);
                reconnectDelayMs = NextReconnectDelayMs(reconnectDelayMs, connectionSettings);
            }
        }

        private static async Task<string?> ReceiveLoopAsync(ClientWebSocket socket, ClientSettings settings, CancellationToken token)
        {
            byte[] buffer = new byte[65536];
            ClientConnectionActivity activity = new ClientConnectionActivity(DateTime.UtcNow);
            using (CancellationTokenSource connectionTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                Task heartbeatTask = SendHeartbeatLoopAsync(socket, settings, activity, connectionTokenSource, connectionTokenSource.Token);
                Task livenessTask = WatchConnectionLivenessAsync(socket, settings, activity, connectionTokenSource, connectionTokenSource.Token);
                try
                {
                    while (!connectionTokenSource.Token.IsCancellationRequested && socket.State == WebSocketState.Open)
                    {
                        WebSocketReceiveResult result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), connectionTokenSource.Token).ConfigureAwait(false);
                        if (result.MessageType == WebSocketMessageType.Close) return activity.FailureReason;

                        string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        activity.MarkServerMessage(DateTime.UtcNow);
                        ClientConnectionState.MarkServerMessage(settings);
                        PrintServerMessage(message);
                        await HandleServerMessageAsync(socket, settings, message, connectionTokenSource.Token).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested && !String.IsNullOrWhiteSpace(activity.FailureReason))
                {
                    return activity.FailureReason;
                }
                catch (WebSocketException) when (!String.IsNullOrWhiteSpace(activity.FailureReason))
                {
                    return activity.FailureReason;
                }
                catch (InvalidOperationException) when (!String.IsNullOrWhiteSpace(activity.FailureReason))
                {
                    return activity.FailureReason;
                }
                catch (IOException) when (!String.IsNullOrWhiteSpace(activity.FailureReason))
                {
                    return activity.FailureReason;
                }
                finally
                {
                    CancelActiveRequests();
                    LocalInferenceTunnelBridge.Clear();
                    connectionTokenSource.Cancel();
                    await ObserveConnectionTaskAsync(heartbeatTask).ConfigureAwait(false);
                    await ObserveConnectionTaskAsync(livenessTask).ConfigureAwait(false);
                }

                return activity.FailureReason;
            }
        }

        private static async Task SendHeartbeatLoopAsync(ClientWebSocket socket, ClientSettings settings, ClientConnectionActivity activity, CancellationTokenSource connectionTokenSource, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(settings.Server.HeartbeatIntervalMs), token).ConfigureAwait(false);
                    if (token.IsCancellationRequested || socket.State != WebSocketState.Open) return;

                    await SendMessageAsync(socket, new { type = "client.heartbeat", clientId = settings.ClientId, tenantId = settings.TenantId, sentUtc = DateTime.UtcNow, networkCapacity = Math.Max(0, ConcurrencyLimitResolver.NetworkCapacity(settings) - ProviderConcurrencyGate.Shared.LocalInFlightOnSharedProviders(settings)) }, token).ConfigureAwait(false);
                    ClientConnectionState.MarkHeartbeat(settings);
                    if (ClientConnectionState.IsInventoryUpdatePending())
                    {
                        ClientSettings latestSettings = LoadRuntimeSettingsForConnectedSession(settings);
                        await SendMessageAsync(socket, new ClientInventoryUpdateMessage
                        {
                            Type = "client.inventory.update",
                            ClientId = latestSettings.ClientId,
                            TenantId = latestSettings.TenantId,
                            SentUtc = DateTime.UtcNow,
                            Inventory = latestSettings.BuildInventoryMessage()
                        }, token).ConfigureAwait(false);
                        ClientConnectionState.MarkInventoryUpdateSent();
                        WriteInfo("Sent client inventory update to Xeno server.");
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    return;
                }
                catch (InvalidOperationException ex)
                {
                    FailConnection(socket, activity, connectionTokenSource, "Heartbeat failed: " + ex.Message);
                    return;
                }
                catch (WebSocketException ex)
                {
                    FailConnection(socket, activity, connectionTokenSource, "Heartbeat failed: " + ex.Message);
                    return;
                }
                catch (IOException ex)
                {
                    FailConnection(socket, activity, connectionTokenSource, "Heartbeat failed: " + ex.Message);
                    return;
                }
            }
        }

        private static async Task WatchConnectionLivenessAsync(ClientWebSocket socket, ClientSettings settings, ClientConnectionActivity activity, CancellationTokenSource connectionTokenSource, CancellationToken token)
        {
            int pollIntervalMs = Math.Clamp(settings.Server.HeartbeatIntervalMs / 2, 1000, 5000);
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(pollIntervalMs), token).ConfigureAwait(false);
                    if (token.IsCancellationRequested || socket.State != WebSocketState.Open) return;
                    if (!activity.IsStale(DateTime.UtcNow, settings.Server.LivenessTimeoutMs)) continue;

                    FailConnection(socket, activity, connectionTokenSource, "No server WebSocket message received within " + settings.Server.LivenessTimeoutMs.ToString(CultureInfo.InvariantCulture) + "ms.");
                    return;
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    return;
                }
            }
        }

        private static void FailConnection(ClientWebSocket socket, ClientConnectionActivity activity, CancellationTokenSource connectionTokenSource, string reason)
        {
            if (!activity.TryFail(reason)) return;
            try
            {
                socket.Abort();
            }
            catch (ObjectDisposedException)
            {
            }

            connectionTokenSource.Cancel();
        }

        private static async Task ObserveConnectionTaskAsync(Task task)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (WebSocketException)
            {
            }
            catch (InvalidOperationException)
            {
            }
            catch (IOException)
            {
            }
        }

        private static async Task DelayBeforeReconnectAsync(int reconnectDelayMs, ClientSettings settings, CancellationToken token)
        {
            int jitterMs = settings.Server.ReconnectJitterMs <= 0 ? 0 : Random.Shared.Next(0, settings.Server.ReconnectJitterMs + 1);
            int delayMs = Math.Max(0, reconnectDelayMs) + jitterMs;
            if (delayMs <= 0) return;
            await Task.Delay(TimeSpan.FromMilliseconds(delayMs), token).ConfigureAwait(false);
        }

        private static int NextReconnectDelayMs(int currentDelayMs, ClientSettings settings)
        {
            int maximumDelayMs = Math.Max(settings.Server.ReconnectInitialDelayMs, settings.Server.ReconnectMaxDelayMs);
            if (currentDelayMs <= 0) return Math.Min(1000, maximumDelayMs);
            long nextDelayMs = (long)currentDelayMs * 2;
            if (nextDelayMs > maximumDelayMs) return maximumDelayMs;
            return (int)nextDelayMs;
        }

        private static async Task HandleServerMessageAsync(ClientWebSocket socket, ClientSettings settings, string message, CancellationToken token)
        {
            WebSocketMessageEnvelope? envelope = JsonUtil.Deserialize<WebSocketMessageEnvelope>(message);
            string type = envelope?.Type ?? string.Empty;
            if (String.Equals(type, "server.registered", StringComparison.OrdinalIgnoreCase))
            {
                LocalInferenceTunnelBridge.Register(settings, async (payload, sendToken) => await SendMessageAsync(socket, payload, sendToken).ConfigureAwait(false));
                return;
            }

            if (String.Equals(type, "server.telemetry.request", StringComparison.OrdinalIgnoreCase))
            {
                ClientSettings runtimeSettings = LoadRuntimeSettingsForConnectedSession(settings);
                await HandleServerTelemetryRequestAsync(socket, runtimeSettings, message, token).ConfigureAwait(false);
                return;
            }

            if (String.Equals(type, "server.modelManagement.request", StringComparison.OrdinalIgnoreCase))
            {
                ClientSettings runtimeSettings = LoadRuntimeSettingsForConnectedSession(settings);
                StartServerModelManagementTask(socket, runtimeSettings, message, token);
                return;
            }

            if (String.Equals(type, "server.performanceTest.request", StringComparison.OrdinalIgnoreCase))
            {
                ClientSettings runtimeSettings = LoadRuntimeSettingsForConnectedSession(settings);
                StartServerPerformanceTestTask(socket, runtimeSettings, message, token);
                return;
            }

            if (String.Equals(type, "server.performanceTest.relay.response", StringComparison.OrdinalIgnoreCase))
            {
                ServerPerformanceTestRelayResponseMessage? response = JsonUtil.Deserialize<ServerPerformanceTestRelayResponseMessage>(message);
                if (response != null) CompletePerformanceRelayResponse(response);
                return;
            }

            if (String.Equals(type, "server.localInference.response.start", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(type, "server.localInference.response.complete", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(type, "server.localInference.response", StringComparison.OrdinalIgnoreCase))
            {
                ServerLocalInferenceResponseMessage? response = JsonUtil.Deserialize<ServerLocalInferenceResponseMessage>(message);
                if (response != null) LocalInferenceTunnelBridge.AcceptResponse(response);
                return;
            }

            if (String.Equals(type, "server.localInference.response.chunk", StringComparison.OrdinalIgnoreCase))
            {
                ServerLocalInferenceChunkMessage? chunk = JsonUtil.Deserialize<ServerLocalInferenceChunkMessage>(message);
                if (chunk != null) LocalInferenceTunnelBridge.AcceptChunk(chunk);
                return;
            }

            if (String.Equals(type, "server.request.cancel", StringComparison.OrdinalIgnoreCase))
            {
                HandleServerRequestCancellation(message);
                return;
            }

            if (!String.Equals(type, "server.request", StringComparison.OrdinalIgnoreCase)) return;

            StartServerRequestTask(socket, settings, message, token);
        }

        private static async Task HandleServerTelemetryRequestAsync(ClientWebSocket socket, ClientSettings settings, string message, CancellationToken token)
        {
            ServerTelemetryCollectionRequestMessage? request = JsonUtil.Deserialize<ServerTelemetryCollectionRequestMessage>(message);
            if (request == null) return;
            string tenantId = settings.TenantId ?? string.Empty;
            string clientId = settings.ClientId ?? string.Empty;

            if (!String.Equals(request.TenantId, tenantId, StringComparison.OrdinalIgnoreCase) ||
                !String.Equals(request.ClientId, clientId, StringComparison.OrdinalIgnoreCase))
            {
                await SendMessageAsync(socket, new ClientTelemetryErrorMessage
                {
                    RequestId = request.RequestId,
                    TenantId = tenantId,
                    ClientId = clientId,
                    ErrorCode = "TelemetryIdentityMismatch",
                    ErrorMessage = "Telemetry request tenant or client did not match this client."
                }, token).ConfigureAwait(false);
                return;
            }

            if (request.CollectHostTelemetry)
            {
                try
                {
                    ClientTelemetryCommandRunner commandRunner = new ClientTelemetryCommandRunner();
                    ClientTelemetryService service = new ClientTelemetryService(new ClientSystemTelemetryProvider(commandRunner), new ClientGpuTelemetryProvider(commandRunner));
                    Xeno.Client.Telemetry.Models.ClientTelemetrySnapshot localSnapshot = await service.CollectAsync(settings, token).ConfigureAwait(false);
                    ClientTelemetrySnapshot? snapshot = JsonUtil.Deserialize<ClientTelemetrySnapshot>(JsonUtil.Serialize(localSnapshot));
                    if (snapshot != null)
                    {
                        await SendMessageAsync(socket, new ClientTelemetrySnapshotMessage
                        {
                            RequestId = request.RequestId,
                            TenantId = tenantId,
                            ClientId = clientId,
                            Snapshot = snapshot
                        }, token).ConfigureAwait(false);
                    }
                }
                catch (Exception ex) when (ex is InvalidOperationException || ex is IOException || ex is UnauthorizedAccessException || ex is TimeoutException)
                {
                    await SendMessageAsync(socket, new ClientTelemetryErrorMessage
                    {
                        RequestId = request.RequestId,
                        TenantId = tenantId,
                        ClientId = clientId,
                        ErrorCode = "HostTelemetryFailed",
                        ErrorMessage = ex.Message
                    }, token).ConfigureAwait(false);
                }
            }

            if (request.CollectProviderTelemetry)
            {
                try
                {
                    ClientProviderTelemetryService service = new ClientProviderTelemetryService(new PrometheusMetricsParser());
                    Xeno.Client.Telemetry.Models.ClientProviderTelemetrySnapshot localSnapshot = await service.CollectAsync(settings, token).ConfigureAwait(false);
                    ClientProviderTelemetrySnapshot? snapshot = JsonUtil.Deserialize<ClientProviderTelemetrySnapshot>(JsonUtil.Serialize(localSnapshot));
                    if (snapshot != null)
                    {
                        await SendMessageAsync(socket, new ClientProviderTelemetrySnapshotMessage
                        {
                            RequestId = request.RequestId,
                            TenantId = tenantId,
                            ClientId = clientId,
                            Snapshot = snapshot
                        }, token).ConfigureAwait(false);
                    }
                }
                catch (Exception ex) when (ex is InvalidOperationException || ex is IOException || ex is UnauthorizedAccessException || ex is TimeoutException)
                {
                    await SendMessageAsync(socket, new ClientTelemetryErrorMessage
                    {
                        Type = "client.providerTelemetry.error",
                        RequestId = request.RequestId,
                        TenantId = tenantId,
                        ClientId = clientId,
                        ErrorCode = "ProviderTelemetryFailed",
                        ErrorMessage = ex.Message
                    }, token).ConfigureAwait(false);
                }
            }
        }

        private static void StartServerModelManagementTask(ClientWebSocket socket, ClientSettings settings, string message, CancellationToken token)
        {
            ServerModelManagementRequestMessage? request = JsonUtil.Deserialize<ServerModelManagementRequestMessage>(message);
            if (request == null || String.IsNullOrWhiteSpace(request.RequestId)) return;
            CancellationTokenSource requestTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
            _ = ProcessServerModelManagementAsync(socket, settings, request, requestTokenSource);
        }

        private static async Task ProcessServerModelManagementAsync(ClientWebSocket socket, ClientSettings settings, ServerModelManagementRequestMessage request, CancellationTokenSource requestTokenSource)
        {
            try
            {
                ClientModelManagementResponseMessage response = await ExecuteServerModelManagementAsync(socket, settings, request, requestTokenSource.Token).ConfigureAwait(false);
                await SendMessageAsync(socket, response, requestTokenSource.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is IOException || ex is HttpRequestException || ex is TaskCanceledException || ex is OperationCanceledException)
            {
                await SendMessageAsync(socket, new ClientModelManagementResponseMessage
                {
                    Type = "client.modelManagement.response",
                    RequestId = request.RequestId,
                    TenantId = settings.TenantId ?? String.Empty,
                    ClientId = settings.ClientId ?? String.Empty,
                    Operation = request.Operation,
                    Model = request.Model,
                    Source = request.Source,
                    Success = false,
                    Message = ex.Message,
                    Error = ex.Message
                }, CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                requestTokenSource.Dispose();
            }
        }

        private static async Task<ClientModelManagementResponseMessage> ExecuteServerModelManagementAsync(ClientWebSocket socket, ClientSettings settings, ServerModelManagementRequestMessage request, CancellationToken token)
        {
            ValidateModelManagementIdentity(settings, request);
            string operation = NormalizeModelManagementOperation(request.Operation);
            if (String.IsNullOrWhiteSpace(operation)) throw new InvalidOperationException("Unsupported model management operation: " + request.Operation);
            if (String.IsNullOrWhiteSpace(request.Model)) throw new InvalidOperationException("Model management request model is required.");

            ClientRunnerSettings runner = FindModelManagementRunner(settings, request.Source)
                ?? throw new InvalidOperationException("No Ollama provider with AllowModelManagement enabled was found for this request.");

            if (String.Equals(operation, "pull", StringComparison.OrdinalIgnoreCase))
            {
                return await ExecuteOllamaModelPullAsync(socket, settings, runner, request, token).ConfigureAwait(false);
            }

            return await ExecuteOllamaModelDeleteAsync(socket, settings, runner, request, token).ConfigureAwait(false);
        }

        private static void ValidateModelManagementIdentity(ClientSettings settings, ServerModelManagementRequestMessage request)
        {
            string tenantId = settings.TenantId ?? String.Empty;
            string clientId = settings.ClientId ?? String.Empty;
            if (!String.Equals(request.TenantId, tenantId, StringComparison.OrdinalIgnoreCase) ||
                !String.Equals(request.ClientId, clientId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Model management request tenant or client did not match this client.");
            }
        }

        private static string NormalizeModelManagementOperation(string? operation)
        {
            if (String.Equals(operation, "pull", StringComparison.OrdinalIgnoreCase)) return "pull";
            if (String.Equals(operation, "delete", StringComparison.OrdinalIgnoreCase)) return "delete";
            return String.Empty;
        }

        private static ClientRunnerSettings? FindModelManagementRunner(ClientSettings settings, string? source)
        {
            IEnumerable<ClientRunnerSettings> runners = String.IsNullOrWhiteSpace(source)
                ? settings.Runners
                : settings.Runners.Where(item => String.Equals(item.Name, source, StringComparison.OrdinalIgnoreCase));
            return runners.FirstOrDefault(item =>
                String.Equals(item.ApiType, "Ollama", StringComparison.OrdinalIgnoreCase) &&
                item.AllowModelManagement);
        }

        private static async Task<ClientModelManagementResponseMessage> ExecuteOllamaModelPullAsync(ClientWebSocket socket, ClientSettings settings, ClientRunnerSettings runner, ServerModelManagementRequestMessage request, CancellationToken token)
        {
            await SendModelManagementProgressAsync(socket, settings, request, "started", "starting", "Starting Ollama model pull.", null, null, null, null, token).ConfigureAwait(false);
            string endpoint = runner.Endpoint.TrimEnd('/') + "/api/pull";
            string requestBody = JsonUtil.Serialize(new OllamaModelPullRequest
            {
                Model = request.Model,
                Stream = true,
                Insecure = request.Insecure
            });
            string responseBody = String.Empty;
            int statusCode = 0;
            using (HttpClient httpClient = new HttpClient())
            using (HttpRequestMessage httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint))
            {
                httpClient.Timeout = TimeSpan.FromMilliseconds(Math.Max(1000, request.TimeoutMs > 0 ? request.TimeoutMs : runner.TimeoutMs));
                if (!String.IsNullOrWhiteSpace(runner.Credential))
                {
                    httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", runner.Credential);
                }

                httpRequest.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                using (HttpResponseMessage response = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false))
                {
                    statusCode = (int)response.StatusCode;
                    using (Stream responseStream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
                    using (StreamReader reader = new StreamReader(responseStream, Encoding.UTF8))
                    {
                        StringBuilder captured = new StringBuilder();
                        while (true)
                        {
                            string? line = await reader.ReadLineAsync(token).ConfigureAwait(false);
                            if (line == null) break;
                            if (String.IsNullOrWhiteSpace(line)) continue;
                            captured.AppendLine(line);
                            OllamaModelPullProgress? progress = null;
                            JsonUtil.TryDeserialize(line, out progress);
                            string status = progress?.Status ?? line;
                            await SendModelManagementProgressAsync(socket, settings, request, "progress", status, status, progress?.Completed, progress?.Total, progress?.Digest, line, token).ConfigureAwait(false);
                        }

                        responseBody = captured.ToString();
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        string message = String.IsNullOrWhiteSpace(responseBody) ? response.ReasonPhrase ?? "Ollama pull failed." : responseBody.Trim();
                        return BuildModelManagementResponse(settings, request, false, statusCode, message, message, responseBody);
                    }
                }
            }

            ClientSettings persistedSettings = SavePulledModelSettings(settings, runner, request.Model);
            await LocalDashboardRuntime.RefreshSettingsAsync(persistedSettings, token).ConfigureAwait(false);
            await SendClientInventoryUpdateAsync(socket, persistedSettings, token).ConfigureAwait(false);
            await SendModelManagementProgressAsync(socket, settings, request, "completed", "success", "Ollama model pull completed.", null, null, null, null, token).ConfigureAwait(false);
            return BuildModelManagementResponse(settings, request, true, statusCode, "Ollama model pull completed.", null, responseBody);
        }

        private static async Task<ClientModelManagementResponseMessage> ExecuteOllamaModelDeleteAsync(ClientWebSocket socket, ClientSettings settings, ClientRunnerSettings runner, ServerModelManagementRequestMessage request, CancellationToken token)
        {
            await SendModelManagementProgressAsync(socket, settings, request, "started", "starting", "Deleting Ollama model.", null, null, null, null, token).ConfigureAwait(false);
            string endpoint = runner.Endpoint.TrimEnd('/') + "/api/delete";
            string requestBody = JsonUtil.Serialize(new OllamaModelDeleteRequest { Model = request.Model });
            string responseBody = String.Empty;
            int statusCode;
            using (HttpClient httpClient = new HttpClient())
            using (HttpRequestMessage httpRequest = new HttpRequestMessage(HttpMethod.Delete, endpoint))
            {
                httpClient.Timeout = TimeSpan.FromMilliseconds(Math.Max(1000, request.TimeoutMs > 0 ? request.TimeoutMs : runner.TimeoutMs));
                if (!String.IsNullOrWhiteSpace(runner.Credential))
                {
                    httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", runner.Credential);
                }

                httpRequest.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                using (HttpResponseMessage response = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseContentRead, token).ConfigureAwait(false))
                {
                    statusCode = (int)response.StatusCode;
                    responseBody = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        string message = String.IsNullOrWhiteSpace(responseBody) ? response.ReasonPhrase ?? "Ollama delete failed." : responseBody.Trim();
                        return BuildModelManagementResponse(settings, request, false, statusCode, message, message, responseBody);
                    }
                }
            }

            int removed = runner.Models.RemoveAll(item => String.Equals(item.Name, request.Model, StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
            {
                SaveInventorySettings(settings);
                await SendClientInventoryUpdateAsync(socket, settings, token).ConfigureAwait(false);
            }

            await SendModelManagementProgressAsync(socket, settings, request, "completed", "success", "Ollama model delete completed.", null, null, null, null, token).ConfigureAwait(false);
            return BuildModelManagementResponse(settings, request, true, statusCode, "Ollama model delete completed.", null, responseBody);
        }

        private static ClientSettings SavePulledModelSettings(ClientSettings runtimeSettings, ClientRunnerSettings runtimeRunner, string modelName)
        {
            ClientSettings latestSettings = LoadRuntimeSettingsForConnectedSession(runtimeSettings);
            ClientRunnerSettings latestRunner = latestSettings.Runners.FirstOrDefault(item => String.Equals(item.Name, runtimeRunner.Name, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("The Ollama provider selected for model pull is no longer configured: " + runtimeRunner.Name);
            AddPulledModel(latestRunner, modelName);
            SaveInventorySettings(latestSettings);
            runtimeSettings.Providers = latestSettings.Providers;
            return latestSettings;
        }

        private static void AddPulledModel(ClientRunnerSettings runner, string modelName)
        {
            runner.Models ??= new List<ClientModelSettings>();
            ClientModelSettings? model = runner.Models.FirstOrDefault(item => String.Equals(item.Name, modelName, StringComparison.OrdinalIgnoreCase));
            if (model != null)
            {
                model.SupportsCompletions = true;
                model.SupportsEmbeddings = false;
                return;
            }

            runner.Models.Add(new ClientModelSettings
            {
                Name = modelName,
                SupportsCompletions = true,
                SupportsEmbeddings = false
            });
        }

        private static ClientModelManagementResponseMessage BuildModelManagementResponse(ClientSettings settings, ServerModelManagementRequestMessage request, bool success, int statusCode, string message, string? error, string? body)
        {
            return new ClientModelManagementResponseMessage
            {
                Type = "client.modelManagement.response",
                RequestId = request.RequestId,
                TenantId = settings.TenantId ?? String.Empty,
                ClientId = settings.ClientId ?? String.Empty,
                Operation = NormalizeModelManagementOperation(request.Operation),
                Model = request.Model,
                Source = request.Source,
                Success = success,
                StatusCode = statusCode,
                Message = message,
                Error = error,
                Body = body,
                SentUtc = DateTime.UtcNow
            };
        }

        private static async Task SendModelManagementProgressAsync(
            ClientWebSocket socket,
            ClientSettings settings,
            ServerModelManagementRequestMessage request,
            string eventType,
            string? status,
            string message,
            long? completed,
            long? total,
            string? digest,
            string? rawLine,
            CancellationToken token)
        {
            await SendMessageAsync(socket, new ClientModelManagementProgressMessage
            {
                Type = "client.modelManagement.progress",
                RequestId = request.RequestId,
                TenantId = settings.TenantId ?? String.Empty,
                ClientId = settings.ClientId ?? String.Empty,
                Operation = NormalizeModelManagementOperation(request.Operation),
                Model = request.Model,
                Source = request.Source,
                EventType = eventType,
                Status = status,
                Message = message,
                Completed = completed,
                Total = total,
                Digest = digest,
                RawLine = rawLine,
                SentUtc = DateTime.UtcNow
            }, token).ConfigureAwait(false);
        }

        private static async Task SendClientInventoryUpdateAsync(ClientWebSocket socket, ClientSettings settings, CancellationToken token)
        {
            await SendMessageAsync(socket, new ClientInventoryUpdateMessage
            {
                Type = "client.inventory.update",
                ClientId = settings.ClientId,
                TenantId = settings.TenantId,
                SentUtc = DateTime.UtcNow,
                Inventory = settings.BuildInventoryMessage()
            }, token).ConfigureAwait(false);
            ClientConnectionState.MarkInventoryUpdateSent();
            WriteInfo("Sent client inventory update to Xeno server.");
        }

        private static void StartServerPerformanceTestTask(ClientWebSocket socket, ClientSettings settings, string message, CancellationToken token)
        {
            ServerPerformanceTestRequestMessage? request = JsonUtil.Deserialize<ServerPerformanceTestRequestMessage>(message);
            if (request == null) return;
            CancellationTokenSource requestTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
            _ = ProcessServerPerformanceTestAsync(socket, settings, request, requestTokenSource);
        }

        private static async Task ProcessServerPerformanceTestAsync(ClientWebSocket socket, ClientSettings settings, ServerPerformanceTestRequestMessage request, CancellationTokenSource requestTokenSource)
        {
            try
            {
                ClientPerformanceTestResponseMessage response = String.Equals(request.Mode, "relay", StringComparison.OrdinalIgnoreCase)
                    ? await RunRelayPerformanceTestAsync(socket, settings, request, requestTokenSource.Token).ConfigureAwait(false)
                    : await RunDirectPerformanceTestAsync(socket, settings, request, requestTokenSource.Token).ConfigureAwait(false);
                await SendMessageAsync(socket, response, requestTokenSource.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is IOException || ex is UnauthorizedAccessException || ex is OperationCanceledException)
            {
                ClientPerformanceTestResponseMessage response = new ClientPerformanceTestResponseMessage
                {
                    Type = "client.performanceTest.response",
                    RequestId = request.RequestId,
                    TenantId = settings.TenantId ?? String.Empty,
                    ClientId = settings.ClientId ?? String.Empty,
                    Mode = request.Mode,
                    Success = false,
                    Error = ex.Message,
                    Result = new ModelPerformanceTestLegResult
                    {
                        Name = request.Mode,
                        Description = ex.Message
                    }
                };
                await SendMessageAsync(socket, response, CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                requestTokenSource.Dispose();
            }
        }

        private static async Task<ClientPerformanceTestResponseMessage> RunDirectPerformanceTestAsync(ClientWebSocket socket, ClientSettings settings, ServerPerformanceTestRequestMessage request, CancellationToken token)
        {
            int requestCount = Math.Clamp(request.RequestCount, 1, 10);
            ModelPerformanceTestLegResult result = new ModelPerformanceTestLegResult
            {
                Name = "native",
                Description = "Ran " + requestCount.ToString(CultureInfo.InvariantCulture) + " " + request.RequestType + " request(s) from the system hosting the model."
            };

            await SendPerformanceProgressAsync(socket, settings, request, "direct", "legStarted", "Starting native provider samples on this model host.", null, result, 0, requestCount, token).ConfigureAwait(false);
            await SendPerformanceProgressAsync(socket, settings, request, "direct", "warmupStarted", "Running unmeasured native provider warmup request.", null, result, 0, requestCount, token).ConfigureAwait(false);
            ModelPerformanceTestSampleResult warmup;
            try
            {
                warmup = await ExecutePerformanceProviderSampleAsync(settings, request, 0, token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is IOException || ex is HttpRequestException || ex is TaskCanceledException)
            {
                warmup = FailedPerformanceSample(0, ex.Message);
            }

            await SendPerformanceProgressAsync(socket, settings, request, "direct", "warmupCompleted", PerformanceWarmupProgressMessage("Native", warmup), warmup, result, 0, requestCount, token).ConfigureAwait(false);
            for (int index = 1; index <= requestCount; index++)
            {
                token.ThrowIfCancellationRequested();
                ModelPerformanceTestSampleResult sample;
                try
                {
                    sample = await ExecutePerformanceProviderSampleAsync(settings, request, index, token).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is InvalidOperationException || ex is IOException || ex is HttpRequestException || ex is TaskCanceledException)
                {
                    sample = FailedPerformanceSample(index, ex.Message);
                }

                result.Samples.Add(sample);
                result.Summary = SummarizePerformanceSamples(result.Samples);
                await SendPerformanceProgressAsync(socket, settings, request, "direct", "sampleCompleted", PerformanceSampleProgressMessage("Native", sample), sample, result, result.Samples.Count, requestCount, token).ConfigureAwait(false);
            }

            result.Summary = SummarizePerformanceSamples(result.Samples);
            await SendPerformanceProgressAsync(socket, settings, request, "direct", "legCompleted", "Native provider samples completed.", null, result, result.Samples.Count, requestCount, token).ConfigureAwait(false);
            return new ClientPerformanceTestResponseMessage
            {
                Type = "client.performanceTest.response",
                RequestId = request.RequestId,
                TenantId = settings.TenantId ?? String.Empty,
                ClientId = settings.ClientId ?? String.Empty,
                Mode = "direct",
                Success = result.Samples.Any(item => item.Success),
                Result = result
            };
        }

        private static async Task<ClientPerformanceTestResponseMessage> RunRelayPerformanceTestAsync(ClientWebSocket socket, ClientSettings settings, ServerPerformanceTestRequestMessage request, CancellationToken token)
        {
            int requestCount = Math.Clamp(request.RequestCount, 1, 10);
            ModelPerformanceTestLegResult result = new ModelPerformanceTestLegResult
            {
                Name = "hairpin",
                Description = "Ran " + requestCount.ToString(CultureInfo.InvariantCulture) + " " + request.RequestType + " request(s) from this selected client through Xeno server to " + (request.TargetClientName ?? request.TargetClientId) + "."
            };

            await SendPerformanceProgressAsync(socket, settings, request, "relay", "legStarted", "Starting Xeno samples through Xeno server to " + (request.TargetClientName ?? request.TargetClientId) + ".", null, result, 0, requestCount, token).ConfigureAwait(false);
            await SendPerformanceProgressAsync(socket, settings, request, "relay", "warmupStarted", "Running unmeasured Xeno warmup request through Xeno server.", null, result, 0, requestCount, token).ConfigureAwait(false);
            ModelPerformanceTestSampleResult warmup;
            try
            {
                warmup = await ExecutePerformanceRelaySampleAsync(socket, settings, request, 0, token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is IOException || ex is WebSocketException || ex is OperationCanceledException)
            {
                warmup = FailedPerformanceSample(0, ex.Message);
            }

            await SendPerformanceProgressAsync(socket, settings, request, "relay", "warmupCompleted", PerformanceWarmupProgressMessage("Xeno", warmup), warmup, result, 0, requestCount, token).ConfigureAwait(false);
            for (int index = 1; index <= requestCount; index++)
            {
                token.ThrowIfCancellationRequested();
                ModelPerformanceTestSampleResult sample;
                try
                {
                    sample = await ExecutePerformanceRelaySampleAsync(socket, settings, request, index, token).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is InvalidOperationException || ex is IOException || ex is WebSocketException || ex is OperationCanceledException)
                {
                    sample = FailedPerformanceSample(index, ex.Message);
                }

                result.Samples.Add(sample);
                result.Summary = SummarizePerformanceSamples(result.Samples);
                await SendPerformanceProgressAsync(socket, settings, request, "relay", "sampleCompleted", PerformanceSampleProgressMessage("Xeno", sample), sample, result, result.Samples.Count, requestCount, token).ConfigureAwait(false);
            }

            result.Summary = SummarizePerformanceSamples(result.Samples);
            await SendPerformanceProgressAsync(socket, settings, request, "relay", "legCompleted", "Xeno samples completed.", null, result, result.Samples.Count, requestCount, token).ConfigureAwait(false);
            return new ClientPerformanceTestResponseMessage
            {
                Type = "client.performanceTest.response",
                RequestId = request.RequestId,
                TenantId = settings.TenantId ?? String.Empty,
                ClientId = settings.ClientId ?? String.Empty,
                Mode = "relay",
                Success = result.Samples.Any(item => item.Success),
                Result = result
            };
        }

        private static async Task SendPerformanceProgressAsync(
            ClientWebSocket socket,
            ClientSettings settings,
            ServerPerformanceTestRequestMessage request,
            string mode,
            string eventType,
            string message,
            ModelPerformanceTestSampleResult? sample,
            ModelPerformanceTestLegResult result,
            int completedSamples,
            int totalSamples,
            CancellationToken token)
        {
            await SendMessageAsync(socket, new ClientPerformanceTestProgressMessage
            {
                Type = "client.performanceTest.progress",
                RequestId = request.RequestId,
                TenantId = settings.TenantId ?? String.Empty,
                ClientId = settings.ClientId ?? String.Empty,
                Mode = mode,
                EventType = eventType,
                Message = message,
                Index = sample?.Index,
                CompletedSamples = completedSamples,
                TotalSamples = totalSamples,
                Sample = sample,
                Summary = result.Summary,
                LegResult = String.Equals(eventType, "legCompleted", StringComparison.OrdinalIgnoreCase) ? result : null,
                SentUtc = DateTime.UtcNow
            }, token).ConfigureAwait(false);
        }

        private static string PerformanceSampleProgressMessage(string legName, ModelPerformanceTestSampleResult sample)
        {
            string sampleName = sample.Index.ToString(CultureInfo.InvariantCulture);
            if (!sample.Success)
            {
                return legName + " sample " + sampleName + " failed: " + (sample.Error ?? "request failed");
            }

            string total = sample.TotalTimeMs.HasValue
                ? sample.TotalTimeMs.Value.ToString("0.###", CultureInfo.InvariantCulture) + " ms"
                : "unknown duration";
            string firstToken = sample.TimeToFirstTokenMs.HasValue
                ? ", first token " + sample.TimeToFirstTokenMs.Value.ToString("0.###", CultureInfo.InvariantCulture) + " ms"
                : String.Empty;
            string tokens = sample.TokensPerSecond.HasValue
                ? ", " + sample.TokensPerSecond.Value.ToString("0.###", CultureInfo.InvariantCulture) + " tokens/s"
                : String.Empty;
            return legName + " sample " + sampleName + " completed in " + total + firstToken + tokens + ".";
        }

        private static string PerformanceWarmupProgressMessage(string legName, ModelPerformanceTestSampleResult sample)
        {
            if (!sample.Success)
            {
                return legName + " warmup failed: " + (sample.Error ?? "request failed") + ". Measured samples will still run.";
            }

            string total = sample.TotalTimeMs.HasValue
                ? sample.TotalTimeMs.Value.ToString("0.###", CultureInfo.InvariantCulture) + " ms"
                : "unknown duration";
            string firstToken = sample.TimeToFirstTokenMs.HasValue
                ? ", first token " + sample.TimeToFirstTokenMs.Value.ToString("0.###", CultureInfo.InvariantCulture) + " ms"
                : String.Empty;
            string tokens = sample.TokensPerSecond.HasValue
                ? ", " + sample.TokensPerSecond.Value.ToString("0.###", CultureInfo.InvariantCulture) + " tokens/s"
                : String.Empty;
            return legName + " warmup completed in " + total + firstToken + tokens + ". Measured samples start next.";
        }

        private static async Task<ModelPerformanceTestSampleResult> ExecutePerformanceProviderSampleAsync(ClientSettings settings, ServerPerformanceTestRequestMessage request, int index, CancellationToken token)
        {
            ClientRunnerSettings runner = FindPerformanceRunner(settings, request.TargetSource, request.Model, request.RequestType)
                ?? throw new InvalidOperationException("No configured provider endpoint can run model '" + request.Model + "'.");
            ServerRequestMessage sample = BuildPerformanceSampleRequest(request, runner.ApiType, index);
            string endpoint = runner.Endpoint.TrimEnd('/') + "/" + sample.RelativePath.TrimStart('/');
            Stopwatch stopwatch = Stopwatch.StartNew();
            DateTime startedUtc = DateTime.UtcNow;
            using (HttpClient httpClient = new HttpClient())
            using (HttpRequestMessage httpRequest = new HttpRequestMessage(new HttpMethod(sample.Method), endpoint))
            {
                httpClient.Timeout = TimeSpan.FromMilliseconds(Math.Max(1000, runner.TimeoutMs));
                if (!String.IsNullOrWhiteSpace(runner.Credential))
                {
                    httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", runner.Credential);
                }

                if (!String.Equals(sample.Method, "GET", StringComparison.OrdinalIgnoreCase))
                {
                    string body = PrepareUpstreamRequestBody(runner.ApiType, sample.RelativePath, sample.Body, sample.Stream);
                    httpRequest.Content = new StringContent(body, Encoding.UTF8, "application/json");
                    sample.Body = body;
                }

                HttpCompletionOption completionOption = sample.Stream ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead;
                using (HttpResponseMessage response = await httpClient.SendAsync(httpRequest, completionOption, token).ConfigureAwait(false))
                {
                    if (sample.Stream)
                    {
                        return await ReadPerformanceStreamSampleAsync(response, sample, stopwatch, startedUtc, index, token).ConfigureAwait(false);
                    }

                    string content = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                    stopwatch.Stop();
                    Dictionary<string, string> responseHeaders = ExtractResponseHeaders(response);
                    RequestHistoryTelemetry telemetry = BuildTelemetry(sample.Body, content, responseHeaders, stopwatch.Elapsed.TotalMilliseconds, null, null, 0, 0, false);
                    return SampleFromTelemetry(index, response.IsSuccessStatusCode, (int)response.StatusCode, telemetry, Math.Round(stopwatch.Elapsed.TotalMilliseconds, 3), startedUtc, null);
                }
            }
        }

        private static async Task<ModelPerformanceTestSampleResult> ReadPerformanceStreamSampleAsync(HttpResponseMessage response, ServerRequestMessage sample, Stopwatch stopwatch, DateTime startedUtc, int index, CancellationToken token)
        {
            Dictionary<string, string> responseHeaders = ExtractResponseHeaders(response);
            double requestToHeadersMs = stopwatch.Elapsed.TotalMilliseconds;
            using (Stream responseStream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
            using (MemoryStream captured = new MemoryStream())
            {
                byte[] buffer = new byte[8192];
                int sequence = 0;
                double? firstByteMs = null;
                while (true)
                {
                    int read = await responseStream.ReadAsync(buffer.AsMemory(0, buffer.Length), token).ConfigureAwait(false);
                    if (read < 1) break;
                    if (!firstByteMs.HasValue) firstByteMs = stopwatch.Elapsed.TotalMilliseconds;
                    await captured.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
                    sequence++;
                }

                stopwatch.Stop();
                string body = Encoding.UTF8.GetString(captured.ToArray());
                int streamEvents = CountStreamEvents(response.Content.Headers.ContentType?.ToString(), body, sequence);
                RequestHistoryTelemetry telemetry = BuildTelemetry(sample.Body, body, responseHeaders, stopwatch.Elapsed.TotalMilliseconds, requestToHeadersMs, firstByteMs, sequence, streamEvents, true);
                return SampleFromTelemetry(index, response.IsSuccessStatusCode, (int)response.StatusCode, telemetry, Math.Round(stopwatch.Elapsed.TotalMilliseconds, 3), startedUtc, null);
            }
        }

        private static async Task<ModelPerformanceTestSampleResult> ExecutePerformanceRelaySampleAsync(ClientWebSocket socket, ClientSettings settings, ServerPerformanceTestRequestMessage request, int index, CancellationToken token)
        {
            ClientRunnerSettings syntheticRunner = new ClientRunnerSettings
            {
                ApiType = request.TargetApiType ?? "OpenAICompatible",
                Name = request.TargetSource ?? "target-provider"
            };
            ServerRequestMessage sample = BuildPerformanceSampleRequest(request, syntheticRunner.ApiType, index);
            string sampleId = Guid.NewGuid().ToString("N");
            TaskCompletionSource<ServerPerformanceTestRelayResponseMessage> pending = new TaskCompletionSource<ServerPerformanceTestRelayResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            _PerformanceRelayResponses[sampleId] = pending;
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                await SendMessageAsync(socket, new ClientPerformanceTestRelayRequestMessage
                {
                    Type = "client.performanceTest.relay.request",
                    RequestId = request.RequestId,
                    SampleId = sampleId,
                    Index = index,
                    SourceClientId = settings.ClientId ?? String.Empty,
                    TargetClientId = request.TargetClientId,
                    TenantId = settings.TenantId ?? String.Empty,
                    TargetApiType = request.TargetApiType,
                    TargetSource = request.TargetSource,
                    Model = request.Model,
                    RequestType = request.RequestType,
                    Method = sample.Method,
                    RelativePath = sample.RelativePath,
                    Stream = sample.Stream,
                    Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                    Body = sample.Body
                }, token).ConfigureAwait(false);

                using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    timeout.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(1000, request.TimeoutMs)));
                    using (timeout.Token.Register(delegate { pending.TrySetCanceled(timeout.Token); }))
                    {
                        ServerPerformanceTestRelayResponseMessage response = await pending.Task.ConfigureAwait(false);
                        stopwatch.Stop();
                        ModelPerformanceTestSampleResult result = response.Result ?? FailedPerformanceSample(index, response.Error ?? "Relay sample failed.");
                        result.TotalTimeMs = Math.Round(stopwatch.Elapsed.TotalMilliseconds, 3);
                        result.TimeToLastTokenMs = result.TotalTimeMs;
                        if (!response.Success && String.IsNullOrWhiteSpace(result.Error)) result.Error = response.Error;
                        return result;
                    }
                }
            }
            finally
            {
                _PerformanceRelayResponses.TryRemove(sampleId, out TaskCompletionSource<ServerPerformanceTestRelayResponseMessage>? _);
            }
        }

        private static void CompletePerformanceRelayResponse(ServerPerformanceTestRelayResponseMessage response)
        {
            if (String.IsNullOrWhiteSpace(response.SampleId)) return;
            if (_PerformanceRelayResponses.TryGetValue(response.SampleId, out TaskCompletionSource<ServerPerformanceTestRelayResponseMessage>? pending))
            {
                pending.TrySetResult(response);
            }
        }

        private static ClientRunnerSettings? FindPerformanceRunner(ClientSettings settings, string? source, string model, string requestType)
        {
            if (!String.IsNullOrWhiteSpace(source))
            {
                ClientRunnerSettings? bySource = settings.Runners.FirstOrDefault(item => String.Equals(item.Name, source, StringComparison.OrdinalIgnoreCase));
                if (bySource != null) return bySource;
            }

            bool embeddings = String.Equals(requestType, "embeddings", StringComparison.OrdinalIgnoreCase);
            return settings.Runners.FirstOrDefault(runner => runner.Models.Any(item =>
                String.Equals(item.Name, model, StringComparison.OrdinalIgnoreCase) &&
                (embeddings ? item.SupportsEmbeddings : item.SupportsCompletions))) ?? settings.Runners.FirstOrDefault();
        }

        private static ServerRequestMessage BuildPerformanceSampleRequest(ServerPerformanceTestRequestMessage request, string apiType, int index)
        {
            bool ollama = String.Equals(apiType, "Ollama", StringComparison.OrdinalIgnoreCase);
            bool embeddings = String.Equals(request.RequestType, "embeddings", StringComparison.OrdinalIgnoreCase);
            bool completions = String.Equals(request.RequestType, "completions", StringComparison.OrdinalIgnoreCase);
            string prompt = "Reply in one short sentence: latency matters. Sample " + index.ToString(CultureInfo.InvariantCulture) + ".";
            if (embeddings)
            {
                return new ServerRequestMessage
                {
                    Method = "POST",
                    RelativePath = ollama ? "/api/embed" : "/v1/embeddings",
                    Model = request.Model,
                    Embeddings = true,
                    Stream = false,
                    Body = ollama
                        ? JsonUtil.Serialize(new { model = request.Model, input = new[] { "local inference performance sample " + index.ToString(CultureInfo.InvariantCulture) } })
                        : JsonUtil.Serialize(new { model = request.Model, input = new[] { "local inference performance sample " + index.ToString(CultureInfo.InvariantCulture) } })
                };
            }

            if (completions)
            {
                return new ServerRequestMessage
                {
                    Method = "POST",
                    RelativePath = ollama ? "/api/generate" : "/v1/completions",
                    Model = request.Model,
                    Stream = true,
                    Body = ollama
                        ? JsonUtil.Serialize(new { model = request.Model, prompt, stream = true })
                        : JsonUtil.Serialize(new { model = request.Model, prompt, stream = true, temperature = 0, max_tokens = 32 })
                };
            }

            return new ServerRequestMessage
            {
                Method = "POST",
                RelativePath = ollama ? "/api/chat" : "/v1/chat/completions",
                Model = request.Model,
                Stream = true,
                Body = JsonUtil.Serialize(new
                {
                    model = request.Model,
                    stream = true,
                    messages = new[]
                    {
                        new { role = "user", content = prompt }
                    },
                    temperature = 0,
                    max_tokens = 32
                })
            };
        }

        private static ModelPerformanceTestSampleResult SampleFromTelemetry(int index, bool success, int? statusCode, RequestHistoryTelemetry telemetry, double totalMs, DateTime startedUtc, string? error)
        {
            return new ModelPerformanceTestSampleResult
            {
                Index = index,
                Success = success,
                StatusCode = statusCode,
                TotalTimeMs = totalMs,
                TimeToFirstTokenMs = telemetry.TimeToFirstTokenMs,
                TimeToLastTokenMs = totalMs,
                TokensPerSecond = CalculateTokensPerSecond(telemetry.CompletionTokens, totalMs),
                PromptTokens = telemetry.PromptTokens,
                CompletionTokens = telemetry.CompletionTokens,
                TotalTokens = telemetry.TotalTokens,
                StreamChunkCount = telemetry.StreamChunkCount,
                RequestBytes = telemetry.RequestBytes,
                ResponseBytes = telemetry.ResponseBytes,
                Error = error,
                StartedUtc = startedUtc
            };
        }

        private static ModelPerformanceTestSampleResult FailedPerformanceSample(int index, string message)
        {
            return new ModelPerformanceTestSampleResult
            {
                Index = index,
                Success = false,
                Error = message,
                StartedUtc = DateTime.UtcNow
            };
        }

        private static double? CalculateTokensPerSecond(int? completionTokens, double? totalMs)
        {
            if (!completionTokens.HasValue || completionTokens.Value <= 0) return null;
            if (!totalMs.HasValue || totalMs.Value <= 0) return null;
            return Math.Round(completionTokens.Value / (totalMs.Value / 1000d), 3);
        }

        private static ModelPerformanceTestSummary SummarizePerformanceSamples(List<ModelPerformanceTestSampleResult> samples)
        {
            List<ModelPerformanceTestSampleResult> rows = samples ?? new List<ModelPerformanceTestSampleResult>();
            return new ModelPerformanceTestSummary
            {
                SuccessCount = rows.Count(item => item.Success),
                FailureCount = rows.Count(item => !item.Success),
                AverageTotalTimeMs = Average(rows.Select(item => item.TotalTimeMs)),
                MedianTotalTimeMs = Median(rows.Select(item => item.TotalTimeMs)),
                MinimumTotalTimeMs = Minimum(rows.Select(item => item.TotalTimeMs)),
                MaximumTotalTimeMs = Maximum(rows.Select(item => item.TotalTimeMs)),
                AverageTimeToFirstTokenMs = Average(rows.Select(item => item.TimeToFirstTokenMs)),
                MedianTimeToFirstTokenMs = Median(rows.Select(item => item.TimeToFirstTokenMs)),
                AverageTimeToLastTokenMs = Average(rows.Select(item => item.TimeToLastTokenMs)),
                AverageTokensPerSecond = Average(rows.Select(item => item.TokensPerSecond)),
                AverageResponseBytes = Average(rows.Select(item => item.ResponseBytes.HasValue ? (double?)item.ResponseBytes.Value : null))
            };
        }

        private static double? Average(IEnumerable<double?> values)
        {
            List<double> usable = values.Where(item => item.HasValue).Select(item => item!.Value).ToList();
            return usable.Count == 0 ? null : Math.Round(usable.Average(), 3);
        }

        private static double? Median(IEnumerable<double?> values)
        {
            List<double> usable = values.Where(item => item.HasValue).Select(item => item!.Value).OrderBy(item => item).ToList();
            if (usable.Count == 0) return null;
            int midpoint = usable.Count / 2;
            if (usable.Count % 2 == 1) return Math.Round(usable[midpoint], 3);
            return Math.Round((usable[midpoint - 1] + usable[midpoint]) / 2d, 3);
        }

        private static double? Minimum(IEnumerable<double?> values)
        {
            List<double> usable = values.Where(item => item.HasValue).Select(item => item!.Value).ToList();
            return usable.Count == 0 ? null : Math.Round(usable.Min(), 3);
        }

        private static double? Maximum(IEnumerable<double?> values)
        {
            List<double> usable = values.Where(item => item.HasValue).Select(item => item!.Value).ToList();
            return usable.Count == 0 ? null : Math.Round(usable.Max(), 3);
        }

        private static void StartServerRequestTask(ClientWebSocket socket, ClientSettings settings, string message, CancellationToken token)
        {
            ServerRequestMessage? serverRequest = JsonUtil.Deserialize<ServerRequestMessage>(message);
            if (serverRequest == null) return;

            string requestId = String.IsNullOrWhiteSpace(serverRequest.RequestId) ? Guid.NewGuid().ToString("N") : serverRequest.RequestId;
            CancellationTokenSource requestTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
            _RequestCancellationSources[requestId] = requestTokenSource;
            _ = ProcessServerRequestAsync(socket, settings, serverRequest, requestId, requestTokenSource);
        }

        private static async Task ProcessServerRequestAsync(ClientWebSocket socket, ClientSettings settings, ServerRequestMessage serverRequest, string requestId, CancellationTokenSource requestTokenSource)
        {
            try
            {
                ClientSettings requestSettings = LoadRuntimeSettingsForConnectedSession(settings);
                object? response = await ExecuteServerRequestAsync(socket, requestSettings, serverRequest, requestId, requestTokenSource.Token).ConfigureAwait(false);
                if (response != null) await SendMessageAsync(socket, response, requestTokenSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (requestTokenSource.IsCancellationRequested)
            {
                if (_RequestCancellationReasons.TryGetValue(requestId, out string? reason) &&
                    String.Equals(reason, "CallerDisconnected", StringComparison.OrdinalIgnoreCase))
                {
                    await LocalDashboardRuntime.RecordCanceledAsync(requestId, "Request canceled due to caller disconnect.", CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                ClientResponseMessage response = new ClientResponseMessage
                {
                    Type = "client.response",
                    RequestId = requestId,
                    ClientId = settings.ClientId,
                    TenantId = settings.TenantId,
                    Success = false,
                    Error = ex.Message
                };
                await LocalDashboardRuntime.RecordCompletedAsync(requestId, response, false, CancellationToken.None).ConfigureAwait(false);
                try
                {
                    await SendMessageAsync(socket, response, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception sendEx) when (sendEx is InvalidOperationException || sendEx is WebSocketException || sendEx is IOException)
                {
                    WriteWarning("Could not send failed request response " + requestId + " to Xeno server: " + sendEx.Message);
                }
            }
            finally
            {
                _RequestCancellationSources.TryRemove(requestId, out CancellationTokenSource? _);
                _RequestCancellationReasons.TryRemove(requestId, out string? _);
                requestTokenSource.Dispose();
            }
        }

        private static async Task<object?> ExecuteServerRequestAsync(ClientWebSocket socket, ClientSettings settings, ServerRequestMessage serverRequest, string requestId, CancellationToken token)
        {
            string? sourceName = String.IsNullOrWhiteSpace(serverRequest.Source) ? serverRequest.RunnerName : serverRequest.Source;
            string proxy = String.IsNullOrWhiteSpace(sourceName) ? "(default provider)" : sourceName;
            string relativePath = String.IsNullOrWhiteSpace(serverRequest.RelativePath) ? serverRequest.Path ?? "/v1/chat/completions" : serverRequest.RelativePath;
            string method = String.IsNullOrWhiteSpace(serverRequest.Method) ? "POST" : serverRequest.Method;
            string requestorIp = RequestorIpFromServerRequest(serverRequest);
            string requestorLabel = String.IsNullOrWhiteSpace(requestorIp) ? "unknown" : requestorIp;
            bool stream = serverRequest.Stream;
            Stopwatch stopwatch = Stopwatch.StartNew();
            ClientRunnerSettings? runner = String.IsNullOrWhiteSpace(sourceName)
                ? settings.Runners.Find(item => item != null && item.ShareWithNetwork)
                : settings.Runners.Find(item => item != null && item.ShareWithNetwork && String.Equals(item.Name, sourceName, StringComparison.OrdinalIgnoreCase));

            await LocalDashboardRuntime.RecordReceivedAsync(serverRequest, runner, requestId, method, relativePath, requestorLabel, token).ConfigureAwait(false);

            if (runner == null)
            {
                stopwatch.Stop();
                WriteRequestCompleted(requestId, requestorLabel, method, relativePath, proxy, stopwatch.Elapsed.TotalMilliseconds);
                ClientResponseMessage response = new ClientResponseMessage
                {
                    Type = "client.response",
                    RequestId = requestId,
                    ClientId = settings.ClientId,
                    TenantId = settings.TenantId,
                    Success = false,
                    Error = "No provider shared with the Xeno network is configured for this request."
                };
                await LocalDashboardRuntime.RecordCompletedAsync(requestId, response, false, token).ConfigureAwait(false);
                return response;
            }

            ConcurrencyLease? lease = ProviderConcurrencyGate.Shared.TryAcquireNetwork(settings, runner, serverRequest.Model);
            if (lease == null)
            {
                stopwatch.Stop();
                WriteRequestCompleted(requestId, requestorLabel, method, relativePath, proxy, stopwatch.Elapsed.TotalMilliseconds);
                ClientResponseMessage busy = new ClientResponseMessage
                {
                    Type = "client.response",
                    RequestId = requestId,
                    ClientId = settings.ClientId,
                    TenantId = settings.TenantId,
                    Success = false,
                    Busy = true,
                    StatusCode = 503,
                    ContentType = "application/json",
                    Body = "{\"error\":\"ClientBusy\",\"message\":\"This client's provider is at its concurrency limit.\"}",
                    Error = "Provider '" + runner.Name + "' is at its concurrency limit."
                };
                await LocalDashboardRuntime.RecordCompletedAsync(requestId, busy, false, token).ConfigureAwait(false);
                return busy;
            }

            try
            {
                Dictionary<string, string> forwardedHeaders = serverRequest.Headers ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                string body = PrepareUpstreamRequestBody(
                    runner.ApiType,
                    relativePath,
                    String.IsNullOrWhiteSpace(serverRequest.Body) ? "{}" : serverRequest.Body,
                    stream);
                string endpoint = runner.Endpoint.TrimEnd('/') + "/" + relativePath.TrimStart('/');
                using (HttpClient httpClient = new HttpClient())
                using (HttpRequestMessage request = new HttpRequestMessage(new HttpMethod(method), endpoint))
                {
                    httpClient.Timeout = TimeSpan.FromMilliseconds(Math.Max(1000, runner.TimeoutMs));
                    if (!String.IsNullOrWhiteSpace(runner.Credential))
                    {
                        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", runner.Credential);
                    }

                    if (!String.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
                    {
                        request.Content = new StringContent(body, Encoding.UTF8, "application/json");
                    }

                    foreach (KeyValuePair<string, string> header in forwardedHeaders)
                    {
                        if (IsBlockedForwardedRequestHeader(header.Key)) continue;
                        if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value) && request.Content != null)
                        {
                            request.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                        }
                    }

                    HttpCompletionOption completionOption = stream ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead;
                    HttpResponseMessage result = await httpClient.SendAsync(request, completionOption, token).ConfigureAwait(false);
                    if (stream)
                    {
                        await StreamUpstreamResponseAsync(socket, settings, requestId, requestorLabel, method, relativePath, proxy, result, body, stopwatch, token).ConfigureAwait(false);
                        return null;
                    }

                    string content = await result.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                    stopwatch.Stop();
                    WriteRequestCompleted(requestId, requestorLabel, method, relativePath, proxy, stopwatch.Elapsed.TotalMilliseconds);
                    Dictionary<string, string> responseHeaders = ExtractResponseHeaders(result);
                    ClientResponseMessage response = new ClientResponseMessage
                    {
                        Type = "client.response",
                        RequestId = requestId,
                        ClientId = settings.ClientId,
                        TenantId = settings.TenantId,
                        Success = result.IsSuccessStatusCode,
                        StatusCode = (int)result.StatusCode,
                        ContentType = result.Content.Headers.ContentType?.ToString(),
                        Headers = responseHeaders,
                        Telemetry = BuildTelemetry(body, content, responseHeaders, stopwatch.Elapsed.TotalMilliseconds, null, null, 0, 0, false),
                        Body = content
                    };
                    await LocalDashboardRuntime.RecordCompletedAsync(requestId, response, false, token).ConfigureAwait(false);
                    return response;
                }
            }
            finally
            {
                lease.Dispose();
            }
        }

        private static async Task StreamUpstreamResponseAsync(
            ClientWebSocket socket,
            ClientSettings settings,
            string requestId,
            string requestorIp,
            string method,
            string relativePath,
            string proxy,
            HttpResponseMessage result,
            string requestBody,
            Stopwatch stopwatch,
            CancellationToken token)
        {
            Dictionary<string, string> responseHeaders = ExtractResponseHeaders(result);
            double requestToHeadersMs = stopwatch.Elapsed.TotalMilliseconds;
            ClientResponseMessage startResponse = new ClientResponseMessage
            {
                Type = "client.response.start",
                RequestId = requestId,
                ClientId = settings.ClientId,
                TenantId = settings.TenantId,
                Success = result.IsSuccessStatusCode,
                StatusCode = (int)result.StatusCode,
                ContentType = result.Content.Headers.ContentType?.ToString(),
                Headers = responseHeaders,
                Telemetry = BuildTelemetry(requestBody, String.Empty, responseHeaders, requestToHeadersMs, requestToHeadersMs, null, 0, 0, true)
            };
            await SendMessageAsync(socket, startResponse, token).ConfigureAwait(false);
            await LocalDashboardRuntime.RecordResponseStartedAsync(requestId, startResponse, token).ConfigureAwait(false);

            using (Stream responseStream = await result.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
            using (MemoryStream captured = new MemoryStream())
            {
                byte[] buffer = new byte[8192];
                int sequence = 0;
                double? firstByteMs = null;

                while (true)
                {
                    int read = await responseStream.ReadAsync(buffer.AsMemory(0, buffer.Length), token).ConfigureAwait(false);
                    if (read < 1) break;

                    if (!firstByteMs.HasValue) firstByteMs = stopwatch.Elapsed.TotalMilliseconds;
                    byte[] chunk = new byte[read];
                    Buffer.BlockCopy(buffer, 0, chunk, 0, read);
                    await captured.WriteAsync(chunk.AsMemory(0, chunk.Length), token).ConfigureAwait(false);
                    sequence++;
                    await SendMessageAsync(socket, new ClientResponseChunkMessage
                    {
                        Type = "client.response.chunk",
                        RequestId = requestId,
                        ClientId = settings.ClientId,
                        TenantId = settings.TenantId,
                        Sequence = sequence,
                        Encoding = "base64",
                        ByteCount = read,
                        ContentBase64 = Convert.ToBase64String(chunk),
                        SentUtc = DateTime.UtcNow
                    }, token).ConfigureAwait(false);
                    await LocalDashboardRuntime.RecordStreamChunkAsync(requestId, sequence, firstByteMs, token).ConfigureAwait(false);
                }

                stopwatch.Stop();
                string body = Encoding.UTF8.GetString(captured.ToArray());
                int streamEvents = CountStreamEvents(result.Content.Headers.ContentType?.ToString(), body, sequence);
                ClientResponseMessage finalResponse = new ClientResponseMessage
                {
                    Type = "client.response.complete",
                    RequestId = requestId,
                    ClientId = settings.ClientId,
                    TenantId = settings.TenantId,
                    Success = result.IsSuccessStatusCode,
                    StatusCode = (int)result.StatusCode,
                    ContentType = result.Content.Headers.ContentType?.ToString(),
                    Headers = responseHeaders,
                    Telemetry = BuildTelemetry(requestBody, body, responseHeaders, stopwatch.Elapsed.TotalMilliseconds, requestToHeadersMs, firstByteMs, sequence, streamEvents, true),
                    Body = body
                };
                await SendMessageAsync(socket, finalResponse, token).ConfigureAwait(false);
                await LocalDashboardRuntime.RecordCompletedAsync(requestId, finalResponse, true, token).ConfigureAwait(false);
                WriteRequestCompleted(requestId, requestorIp, method, relativePath, proxy, stopwatch.Elapsed.TotalMilliseconds);
            }
        }

        private static void HandleServerRequestCancellation(string message)
        {
            ServerRequestCancellationMessage? cancellation = JsonUtil.Deserialize<ServerRequestCancellationMessage>(message);
            if (cancellation == null || String.IsNullOrWhiteSpace(cancellation.RequestId)) return;
            if (!_RequestCancellationSources.TryGetValue(cancellation.RequestId, out CancellationTokenSource? requestTokenSource)) return;

            _RequestCancellationReasons[cancellation.RequestId] = cancellation.Reason;
            requestTokenSource.Cancel();
            if (String.Equals(cancellation.Reason, "CallerDisconnected", StringComparison.OrdinalIgnoreCase))
            {
                string requestorLabel = String.IsNullOrWhiteSpace(cancellation.RequestorIp) ? "unknown" : cancellation.RequestorIp;
                string method = String.IsNullOrWhiteSpace(cancellation.Method) ? "POST" : cancellation.Method;
                string path = String.IsNullOrWhiteSpace(cancellation.RelativePath) ? "/v1/chat/completions" : cancellation.RelativePath;
                string source = String.IsNullOrWhiteSpace(cancellation.Source) ? "(default provider)" : cancellation.Source;
                if (!_UseJson) WriteInfo("Request " + cancellation.RequestId + " canceled due to caller disconnect from " + requestorLabel + ": " + method + " " + path + " via " + source);
            }
        }

        private static void CancelActiveRequests()
        {
            foreach (KeyValuePair<string, CancellationTokenSource> item in _RequestCancellationSources)
            {
                try
                {
                    item.Value.Cancel();
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }

        private static Dictionary<string, string> ExtractResponseHeaders(HttpResponseMessage result)
        {
            Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, IEnumerable<string>> header in result.Headers)
            {
                headers[header.Key] = String.Join(", ", header.Value);
            }

            foreach (KeyValuePair<string, IEnumerable<string>> header in result.Content.Headers)
            {
                headers[header.Key] = String.Join(", ", header.Value);
            }

            return headers;
        }

        private static bool IsBlockedForwardedRequestHeader(string name)
        {
            return name.Equals("Connection", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("Keep-Alive", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("Proxy-Authenticate", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("Proxy-Authorization", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("TE", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("Trailer", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("Upgrade", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("Host", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("Expect", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("Accept-Encoding", StringComparison.OrdinalIgnoreCase);
        }

        private static string PrepareUpstreamRequestBody(string apiType, string relativePath, string body, bool stream)
        {
            if (!ShouldRequestUsageTelemetry(apiType, relativePath, stream)) return body;

            try
            {
                Dictionary<string, object?>? root = JsonSerializer.Deserialize<Dictionary<string, object?>>(body);
                if (root == null) return body;

                Dictionary<string, object?>? streamOptions = null;
                if (root.TryGetValue("stream_options", out object? streamOptionsValue) && streamOptionsValue != null)
                {
                    streamOptions = JsonSerializer.Deserialize<Dictionary<string, object?>>(JsonSerializer.Serialize(streamOptionsValue));
                }

                if (streamOptions == null)
                {
                    streamOptions = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                }

                streamOptions["include_usage"] = true;
                root["stream_options"] = streamOptions;
                return JsonSerializer.Serialize(root);
            }
            catch (JsonException)
            {
                return body;
            }
            catch (InvalidOperationException)
            {
                return body;
            }
        }

        private static bool ShouldRequestUsageTelemetry(string apiType, string relativePath, bool stream)
        {
            if (!stream) return false;
            if (String.IsNullOrWhiteSpace(relativePath)) return false;

            string normalized = relativePath.Trim().ToLowerInvariant();
            if (!normalized.Contains("/v1/", StringComparison.OrdinalIgnoreCase)) return false;
            if (!normalized.EndsWith("/completions", StringComparison.OrdinalIgnoreCase)) return false;

            return IsUsageTelemetryProvider(apiType);
        }

        private static bool IsUsageTelemetryProvider(string apiType)
        {
            if (String.IsNullOrWhiteSpace(apiType)) return true;
            if (String.Equals(apiType, "OpenAI", StringComparison.OrdinalIgnoreCase)) return true;
            if (String.Equals(apiType, "OpenAICompatible", StringComparison.OrdinalIgnoreCase)) return true;
            if (String.Equals(apiType, "vLLM", StringComparison.OrdinalIgnoreCase)) return true;
            if (String.Equals(apiType, "Ollama", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static RequestHistoryTelemetry BuildTelemetry(
            string requestBody,
            string responseBody,
            Dictionary<string, string> responseHeaders,
            double totalMs,
            double? requestToHeadersMs,
            double? firstByteMs,
            int streamChunkCount,
            int streamEventCount,
            bool streaming)
        {
            OpenAiUsage usage = ExtractTokenUsage(responseBody);
            double? streamingMs = firstByteMs.HasValue ? Math.Max(0, totalMs - firstByteMs.Value) : null;
            double? timeToFirst = firstByteMs.HasValue ? Math.Round(firstByteMs.Value) : (streaming ? null : Math.Round(totalMs));
            return new RequestHistoryTelemetry
            {
                TotalTimeMs = (long)Math.Round(totalMs),
                UpstreamTotalMs = (long)Math.Round(totalMs),
                RequestToHeadersMs = requestToHeadersMs.HasValue ? (long)Math.Round(requestToHeadersMs.Value) : null,
                TimeToFirstTokenMs = timeToFirst.HasValue ? (long)timeToFirst.Value : null,
                TotalStreamingTimeMs = streamingMs.HasValue ? (long)Math.Round(streamingMs.Value) : null,
                PromptTokens = usage.PromptTokens,
                CompletionTokens = usage.CompletionTokens,
                TotalTokens = usage.TotalTokens,
                StreamChunkCount = streamChunkCount,
                StreamEventCount = streamEventCount,
                RequestBytes = Encoding.UTF8.GetByteCount(requestBody ?? String.Empty),
                ResponseBytes = Encoding.UTF8.GetByteCount(responseBody ?? String.Empty),
                TransferMode = streaming ? RequestTransferModeEnum.Stream : RequestTransferModeEnum.Standard,
                ProviderRequestId = ExtractProviderRequestId(responseHeaders),
                Metadata = new Dictionary<string, string>
                {
                    ["telemetrySource"] = "xeno.client",
                    ["streaming"] = streaming ? "true" : "false"
                }
            };
        }

        private static string? ExtractProviderRequestId(Dictionary<string, string> headers)
        {
            string? value;
            if (headers.TryGetValue("x-request-id", out value)) return value;
            if (headers.TryGetValue("request-id", out value)) return value;
            if (headers.TryGetValue("x-openai-request-id", out value)) return value;
            if (headers.TryGetValue("x-amzn-requestid", out value)) return value;
            return null;
        }

        private static int CountStreamEvents(string? contentType, string body, int fallback)
        {
            if (String.IsNullOrWhiteSpace(body)) return 0;
            string normalized = (contentType ?? String.Empty).ToLowerInvariant();
            if (normalized.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase) || body.Contains("data:", StringComparison.OrdinalIgnoreCase))
            {
                return body.Split(new[] { "data:" }, StringSplitOptions.None).Length - 1;
            }

            if (normalized.Contains("ndjson", StringComparison.OrdinalIgnoreCase))
            {
                return body.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries).Length;
            }

            return fallback;
        }

        private static OpenAiUsage ExtractTokenUsage(string responseBody)
        {
            if (String.IsNullOrWhiteSpace(responseBody)) return new OpenAiUsage();
            OpenAiUsage usage = ExtractTokenUsageFromJson(responseBody);
            if (usage.HasAny) return usage;

            foreach (string line in responseBody.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) trimmed = trimmed.Substring(5).Trim();
                if (trimmed.Length < 1 || String.Equals(trimmed, "[DONE]", StringComparison.OrdinalIgnoreCase)) continue;
                usage = ExtractTokenUsageFromJson(trimmed);
                if (usage.HasAny) return usage;
            }

            return new OpenAiUsage();
        }

        private static OpenAiUsage ExtractTokenUsageFromJson(string json)
        {
            if (!JsonUtil.TryDeserialize<OpenAiUsageEnvelope>(json, out OpenAiUsageEnvelope? envelope)) return new OpenAiUsage();
            return envelope?.EffectiveUsage ?? new OpenAiUsage();
        }

        private static void ApplyServerAuthentication(ClientWebSocket socket, ClientSettings settings)
        {
            if (!String.IsNullOrWhiteSpace(settings.Server.AccessKey))
            {
                socket.Options.SetRequestHeader("x-access-key", settings.Server.AccessKey);
            }

            socket.Options.SetRequestHeader("x-tenant-guid", settings.TenantId);
        }

        private static void SaveInventorySettings(ClientSettings settings)
        {
            ProviderConfiguration.SaveInventory(settings);
        }

        private static ClientSettings LoadRuntimeSettingsForConnectedSession(ClientSettings connectedSettings)
        {
            ClientSettings latestSettings = ClientSettings.Load();
            if (!String.IsNullOrWhiteSpace(connectedSettings.ClientId)) latestSettings.ClientId = connectedSettings.ClientId;
            if (!String.IsNullOrWhiteSpace(connectedSettings.TenantId)) latestSettings.TenantId = connectedSettings.TenantId;
            return latestSettings;
        }

        private static string ReadSecret(string prompt)
        {
            Console.Write(prompt);
            StringBuilder builder = new StringBuilder();
            while (true)
            {
                ConsoleKeyInfo key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    break;
                }

                if (key.Key == ConsoleKey.Backspace)
                {
                    if (builder.Length > 0) builder.Length -= 1;
                    continue;
                }

                if (!Char.IsControl(key.KeyChar))
                {
                    builder.Append(key.KeyChar);
                }
            }

            return builder.ToString();
        }

        private static async Task SendMessageAsync(ClientWebSocket socket, object value, CancellationToken token)
        {
            string payload = JsonUtil.Serialize(value);
            byte[] bytes = Encoding.UTF8.GetBytes(payload);
            await _SendLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, token).ConfigureAwait(false);
            }
            finally
            {
                _SendLock.Release();
            }
        }

        private static void WriteJson(object value)
        {
            Console.WriteLine(JsonUtil.Serialize(value, true));
        }

        private sealed class TrailingBlankLineTextWriter : TextWriter
        {
            private readonly object _SyncRoot = new object();
            private readonly TextWriter _Inner;
            private bool _HasWritten = false;
            private int _TrailingNewlineCount = 0;

            public TrailingBlankLineTextWriter(TextWriter inner)
            {
                _Inner = inner ?? throw new ArgumentNullException(nameof(inner));
            }

            public TextWriter Inner
            {
                get
                {
                    return _Inner;
                }
            }

            public override Encoding Encoding
            {
                get
                {
                    return _Inner.Encoding;
                }
            }

            public override IFormatProvider FormatProvider
            {
                get
                {
                    return CultureInfo.InvariantCulture;
                }
            }

            public override void Flush()
            {
                lock (_SyncRoot)
                {
                    _Inner.Flush();
                }
            }

            public override Task FlushAsync()
            {
                Flush();
                return Task.CompletedTask;
            }

            public override void Write(char value)
            {
                lock (_SyncRoot)
                {
                    _Inner.Write(value);
                    Track(value);
                }
            }

            public override void Write(char[] buffer, int index, int count)
            {
                lock (_SyncRoot)
                {
                    _Inner.Write(buffer, index, count);
                    Track(buffer.AsSpan(index, count));
                }
            }

            public override void Write(string? value)
            {
                lock (_SyncRoot)
                {
                    _Inner.Write(value);
                    Track(value);
                }
            }

            public override void Write(ReadOnlySpan<char> buffer)
            {
                lock (_SyncRoot)
                {
                    _Inner.Write(buffer);
                    Track(buffer);
                }
            }

            public override void WriteLine()
            {
                lock (_SyncRoot)
                {
                    _Inner.WriteLine();
                    Track(Environment.NewLine);
                }
            }

            public override void WriteLine(string? value)
            {
                lock (_SyncRoot)
                {
                    _Inner.WriteLine(value);
                    Track(value);
                    Track(Environment.NewLine);
                }
            }

            public override void WriteLine(ReadOnlySpan<char> buffer)
            {
                lock (_SyncRoot)
                {
                    _Inner.WriteLine(buffer);
                    Track(buffer);
                    Track(Environment.NewLine);
                }
            }

            public void EnsureTrailingBlankLine()
            {
                lock (_SyncRoot)
                {
                    if (!_HasWritten) return;
                    while (_TrailingNewlineCount < 2)
                    {
                        _Inner.WriteLine();
                        _TrailingNewlineCount++;
                    }

                    _Inner.Flush();
                }
            }

            private void Track(string? value)
            {
                if (String.IsNullOrEmpty(value)) return;
                Track(value.AsSpan());
            }

            private void Track(ReadOnlySpan<char> value)
            {
                for (int i = 0; i < value.Length; i++)
                {
                    Track(value[i]);
                }
            }

            private void Track(char value)
            {
                _HasWritten = true;
                if (value == '\n')
                {
                    _TrailingNewlineCount++;
                    return;
                }

                if (value != '\r')
                {
                    _TrailingNewlineCount = 0;
                }
            }
        }

        private static string RequestorIpFromServerRequest(ServerRequestMessage request)
        {
            string requestorIp = request.RequestorIp ?? String.Empty;
            if (!String.IsNullOrWhiteSpace(requestorIp)) return requestorIp;

            Dictionary<string, string> headers = request.Headers ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string? headerValue;
            if (!headers.TryGetValue("X-Xeno-Requestor-IP", out headerValue) &&
                !headers.TryGetValue("X-Real-IP", out headerValue) &&
                !headers.TryGetValue("X-Forwarded-For", out headerValue))
            {
                return String.Empty;
            }

            requestorIp = headerValue ?? String.Empty;
            int comma = requestorIp.IndexOf(',');
            return comma >= 0 ? requestorIp.Substring(0, comma).Trim() : requestorIp.Trim();
        }

        private static void WriteRequestCompleted(string requestId, string requestorIp, string method, string path, string proxy, double elapsedMs)
        {
            string elapsed = elapsedMs.ToString("0.00", CultureInfo.InvariantCulture);
            WriteInfo("Completed request " + requestId + " for " + requestorIp + ": " + method + " " + path + " via " + proxy + " (" + elapsed + "ms)");
        }

        private static void ConfigureWorkerLogging()
        {
            Directory.CreateDirectory(ClientConnectionState.LogDirectory);
            StreamWriter output = new StreamWriter(new FileStream(ClientConnectionState.LogFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            {
                AutoFlush = true
            };
            StreamWriter error = new StreamWriter(new FileStream(ClientConnectionState.ErrorLogFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            {
                AutoFlush = true
            };
            _WorkerOutputLogWriter = new TimestampedLogWriter(output, "INFO");
            Console.SetOut(_WorkerOutputLogWriter);
            Console.SetError(new TimestampedLogWriter(error, "ERROR"));
            _UseColors = false;
        }

        private sealed class TimestampedLogWriter : TextWriter
        {
            private readonly TextWriter _Inner;
            private readonly string _Severity;
            private readonly object _SyncRoot = new object();
            private bool _AtLineStart = true;

            public TimestampedLogWriter(TextWriter inner, string severity)
            {
                _Inner = inner ?? throw new ArgumentNullException(nameof(inner));
                _Severity = String.IsNullOrWhiteSpace(severity) ? "INFO" : severity.Trim().ToUpperInvariant();
            }

            public override Encoding Encoding
            {
                get
                {
                    return _Inner.Encoding;
                }
            }

            public override IFormatProvider FormatProvider
            {
                get
                {
                    return CultureInfo.InvariantCulture;
                }
            }

            public override void Flush()
            {
                lock (_SyncRoot)
                {
                    _Inner.Flush();
                }
            }

            public override Task FlushAsync()
            {
                Flush();
                return Task.CompletedTask;
            }

            public override void Write(char value)
            {
                lock (_SyncRoot)
                {
                    WritePrefixIfNeeded();
                    _Inner.Write(value);
                    if (value == '\n') _AtLineStart = true;
                }
            }

            public override void Write(string? value)
            {
                if (String.IsNullOrEmpty(value)) return;
                lock (_SyncRoot)
                {
                    for (int i = 0; i < value.Length; i++)
                    {
                        WritePrefixIfNeeded();
                        char item = value[i];
                        _Inner.Write(item);
                        if (item == '\n') _AtLineStart = true;
                    }
                }
            }

            public override void WriteLine()
            {
                lock (_SyncRoot)
                {
                    WritePrefixIfNeeded();
                    _Inner.WriteLine();
                    _AtLineStart = true;
                }
            }

            public override void WriteLine(string? value)
            {
                lock (_SyncRoot)
                {
                    Write(value ?? String.Empty);
                    if (!_AtLineStart)
                    {
                        _Inner.WriteLine();
                        _AtLineStart = true;
                    }
                    else
                    {
                        WriteLine();
                    }

                    _AtLineStart = true;
                }
            }

            public void WriteLineWithSeverity(string severity, string? value)
            {
                lock (_SyncRoot)
                {
                    if (!_AtLineStart)
                    {
                        _Inner.WriteLine();
                    }

                    string normalizedSeverity = String.IsNullOrWhiteSpace(severity) ? _Severity : severity.Trim().ToUpperInvariant();
                    _Inner.Write(FormatLogPrefix(DateTimeOffset.UtcNow, normalizedSeverity));
                    _Inner.WriteLine(value ?? String.Empty);
                    _AtLineStart = true;
                }
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    lock (_SyncRoot)
                    {
                        _Inner.Dispose();
                    }
                }

                base.Dispose(disposing);
            }

            private void WritePrefixIfNeeded()
            {
                if (!_AtLineStart) return;
                _Inner.Write(FormatLogPrefix(DateTimeOffset.UtcNow, _Severity));
                _AtLineStart = false;
            }
        }

        private static string FormatLogPrefix(DateTimeOffset timestamp, string severity)
        {
            string formattedTimestamp = timestamp.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss.", CultureInfo.InvariantCulture) +
                (timestamp.UtcDateTime.Ticks % TimeSpan.TicksPerSecond / 10).ToString("D6", CultureInfo.InvariantCulture) +
                "Z";
            return formattedTimestamp + " [" + severity + "] ";
        }

        private static async Task MonitorStopFileAsync(CancellationTokenSource target, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (ClientConnectionState.IsStopRequested())
                {
                    target.Cancel();
                    return;
                }

                await Task.Delay(500, token).ConfigureAwait(false);
            }
        }

        private static void OpenBrowser(string url)
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                };
                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                WriteWarning("Unable to open browser: " + ex.Message);
            }
        }

        private static bool IsProcessRunning(int processId)
        {
            try
            {
                using (Process process = Process.GetProcessById(processId))
                {
                    return !process.HasExited;
                }
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private static string[] ApplyGlobalOptions(string[] args, ClientSettings? settings)
        {
            _UseColors = settings?.UseColors ?? true;
            _UseJson = false;
            List<string> normalized = new List<string>();
            foreach (string arg in args)
            {
                if (String.Equals(arg, "--no-color", StringComparison.OrdinalIgnoreCase))
                {
                    _UseColors = false;
                    continue;
                }

                if (String.Equals(arg, "--color", StringComparison.OrdinalIgnoreCase))
                {
                    _UseColors = true;
                    continue;
                }

                if (String.Equals(arg, "--json", StringComparison.OrdinalIgnoreCase))
                {
                    _UseJson = true;
                    continue;
                }

                normalized.Add(arg);
            }

            return normalized.ToArray();
        }

        private static void PrintConfig(ClientSettings settings)
        {
            WriteInfoHeading("Xeno client configuration");
            Console.WriteLine("  File:       " + ClientSettings.SettingsFile);
            Console.WriteLine("  Name:       " + settings.Name);
            Console.WriteLine("  Client ID:  " + DisplayValue(settings.ClientId, "(created on first connect)"));
            Console.WriteLine("  Tenant:     " + settings.TenantId);
            Console.WriteLine("  Colors:     " + FormatBool(settings.UseColors));
            WritePlainHeading("Server");
            Console.WriteLine("  WebSocket:  " + settings.Server.Url);
            Console.WriteLine("  REST:       " + DisplayValue(settings.Server.RestUrl, "(derived from WebSocket URL)"));
            Console.WriteLine("  Access key: " + MaskSecret(settings.Server.AccessKey));
            Console.WriteLine("  REST token: " + MaskSecret(settings.Server.Token));
            WritePlainHeading("Local Dashboard");
            Console.WriteLine("  Enabled:    " + FormatBool(settings.LocalDashboard.Enabled));
            Console.WriteLine("  URL:        " + (settings.LocalDashboard.Enabled ? LocalDashboardUrl(settings) : "(disabled)"));
            Console.WriteLine("  Host:       " + settings.LocalDashboard.Host);
            Console.WriteLine("  Port:       " + settings.LocalDashboard.Port.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine("  Database:   " + settings.LocalDashboard.Database.ResolveFilename());
            Console.WriteLine("  Token:      " + (settings.LocalDashboard.RequireLocalToken ? "required" : "not required"));
            PrintProviders(settings, true);
        }

        private static string LocalDashboardUrl(ClientSettings settings)
        {
            string host = String.Equals(settings.LocalDashboard.Host, "0.0.0.0", StringComparison.OrdinalIgnoreCase) ? "127.0.0.1" : settings.LocalDashboard.Host;
            return "http://" + host + ":" + settings.LocalDashboard.Port.ToString(CultureInfo.InvariantCulture);
        }

        private static void PrintInventory(ClientSettings settings)
        {
            WriteInfoHeading("Advertised client inventory");
            Console.WriteLine("  Machine:    " + Environment.MachineName);
            Console.WriteLine("  OS:         " + Environment.OSVersion);
            Console.WriteLine("  Arch:       " + System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture);
            Console.WriteLine("  Processors: " + Environment.ProcessorCount);
            Console.WriteLine("  Memory:     " + FormatBytes(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes));
            Console.WriteLine("  Version:    " + ClientVersion);
            PrintProviders(settings);
            PrintModels(settings);
        }

        private static void PrintServerMessage(string message)
        {
            try
            {
                WebSocketMessageEnvelope? envelope = JsonUtil.Deserialize<WebSocketMessageEnvelope>(message);
                string? type = envelope?.Type;
                if (String.Equals(type, "server.heartbeat", StringComparison.OrdinalIgnoreCase))
                {
                    if (_UseJson) Console.WriteLine(message);
                    return;
                }

                if (String.Equals(type, "server.connected", StringComparison.OrdinalIgnoreCase))
                {
                    if (_UseJson)
                    {
                        Console.WriteLine(message);
                        return;
                    }

                    WriteSuccess("Connected to Xeno server.");
                    return;
                }

                if (String.Equals(type, "server.registered", StringComparison.OrdinalIgnoreCase))
                {
                    ClientConnectionState.MarkRegistered(ClientSettings.Load());
                    if (_UseJson)
                    {
                        Console.WriteLine(message);
                        return;
                    }

                    WriteSuccess("Client registered with Xeno server.");
                    return;
                }

                if (String.Equals(type, "server.request", StringComparison.OrdinalIgnoreCase))
                {
                    if (_UseJson)
                    {
                        Console.WriteLine(message);
                        return;
                    }

                    ServerRequestMessage? request = JsonUtil.Deserialize<ServerRequestMessage>(message);
                    string requestId = String.IsNullOrWhiteSpace(request?.RequestId) ? "(no request ID)" : request.RequestId;
                    string method = String.IsNullOrWhiteSpace(request?.Method) ? "POST" : request.Method;
                    string path = String.IsNullOrWhiteSpace(request?.RelativePath) ? request?.Path ?? "/v1/chat/completions" : request.RelativePath;
                    string source = String.IsNullOrWhiteSpace(request?.Source) ? request?.RunnerName ?? "(default provider)" : request.Source;
                    string requestorIp = request == null ? String.Empty : RequestorIpFromServerRequest(request);
                    string requestorLabel = String.IsNullOrWhiteSpace(requestorIp) ? "unknown" : requestorIp;
                    WriteInfo("Request " + requestId + " received from " + requestorLabel + ": " + method + " " + path + " via " + source);
                    return;
                }

                if (String.Equals(type, "server.request.cancel", StringComparison.OrdinalIgnoreCase))
                {
                    if (_UseJson) Console.WriteLine(message);
                    return;
                }

                if (String.Equals(type, "server.localInference.response.start", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(type, "server.localInference.response.chunk", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(type, "server.localInference.response.complete", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(type, "server.localInference.response", StringComparison.OrdinalIgnoreCase))
                {
                    if (_UseJson) Console.WriteLine(message);
                    return;
                }

                if (String.Equals(type, "server.error", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(type, "server.unauthorized", StringComparison.OrdinalIgnoreCase))
                {
                    if (_UseJson)
                    {
                        Console.WriteLine(message);
                        return;
                    }

                    ClientErrorResponse? error = JsonUtil.Deserialize<ClientErrorResponse>(message);
                    WriteError(error?.Message ?? "The Xeno server rejected the client message.");
                    return;
                }

                if (String.Equals(type, "server.telemetry.request", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(type, "server.telemetry.ack", StringComparison.OrdinalIgnoreCase))
                {
                    if (_UseJson)
                    {
                        Console.WriteLine(message);
                        return;
                    }

                    WriteDebug("Server message: " + type + ".");
                    return;
                }

                if (String.Equals(type, "server.inventory.updated", StringComparison.OrdinalIgnoreCase))
                {
                    if (_UseJson)
                    {
                        Console.WriteLine(message);
                        return;
                    }

                    WriteInfo("Client inventory update acknowledged by Xeno server.");
                    return;
                }

                if (!String.IsNullOrWhiteSpace(type))
                {
                    if (_UseJson)
                    {
                        Console.WriteLine(message);
                        return;
                    }

                    WriteInfo("Server message: " + type + ".");
                    return;
                }
            }
            catch (InvalidDataException)
            {
            }

            if (_UseJson)
            {
                Console.WriteLine(message);
                return;
            }

            WriteInfo("Received a server message.");
        }

        private static void PrintStatus(ClientSettings settings)
        {
            ClientConnectionState connection = ClientConnectionState.Load();
            if (_UseJson)
            {
                WriteJson(new
                {
                    name = settings.Name,
                    clientId = settings.ClientId,
                    tenantId = settings.TenantId,
                    websocketUrl = settings.Server.Url,
                    restUrl = settings.Server.RestUrl,
                    authenticated = ServerEndpoints.HasWebSocketCredential(settings),
                    auth = ServerEndpoints.DescribeAuth(settings),
                    providerCount = settings.Runners.Count,
                    colors = settings.UseColors,
                    connection = new
                    {
                        connected = connection.IsConnected,
                        registered = connection.IsRegistered,
                        background = connection.IsBackground,
                        websocketConnectedUtc = FormatJsonUtc(connection.WebSocketConnectedUtc),
                        connectedUtc = FormatJsonUtc(connection.ConnectedUtc),
                        registeredUtc = FormatJsonUtc(connection.RegisteredUtc),
                        disconnectedUtc = FormatJsonUtc(connection.DisconnectedUtc),
                        lastHeartbeatUtc = FormatJsonUtc(connection.LastHeartbeatUtc),
                        lastServerMessageUtc = FormatJsonUtc(connection.LastServerMessageUtc),
                        lastUpdatedUtc = FormatJsonUtc(connection.LastUpdatedUtc),
                        startedUtc = FormatJsonUtc(connection.StartedUtc),
                        stopRequestedUtc = FormatJsonUtc(connection.StopRequestedUtc),
                        processId = connection.ProcessId ?? 0,
                        logFile = connection.LogFilePath ?? ClientConnectionState.LogFile,
                        errorLogFile = connection.ErrorLogFilePath ?? ClientConnectionState.ErrorLogFile,
                        reason = connection.LastDisconnectReason ?? String.Empty
                    },
                    localDashboard = new
                    {
                        enabled = connection.LocalDashboardEnabled,
                        listening = connection.LocalDashboardListening,
                        url = connection.LocalDashboardUrl ?? String.Empty,
                        host = connection.LocalDashboardHost ?? settings.LocalDashboard.Host,
                        port = connection.LocalDashboardPort ?? settings.LocalDashboard.Port,
                        startedUtc = FormatJsonUtc(connection.LocalDashboardStartedUtc),
                        stoppedUtc = FormatJsonUtc(connection.LocalDashboardStoppedUtc),
                        error = connection.LocalDashboardLastError ?? String.Empty
                    },
                    localInferenceServer = new
                    {
                        enabled = connection.LocalInferenceServerEnabled || settings.LocalInferenceServer.Enabled,
                        listening = connection.LocalInferenceServerListening,
                        url = connection.LocalInferenceServerUrl ?? String.Empty,
                        host = connection.LocalInferenceServerHost ?? settings.LocalInferenceServer.Host,
                        port = connection.LocalInferenceServerPort ?? settings.LocalInferenceServer.Port,
                        startedUtc = FormatJsonUtc(connection.LocalInferenceServerStartedUtc),
                        stoppedUtc = FormatJsonUtc(connection.LocalInferenceServerStoppedUtc),
                        lastRequestUtc = FormatJsonUtc(connection.LocalInferenceServerLastRequestUtc),
                        error = connection.LocalInferenceServerLastError ?? String.Empty,
                        websocketAvailable = connection.IsConnected && connection.IsRegistered
                    }
                });
                return;
            }

            WriteInfoHeading("Xeno client status");
            Console.WriteLine("  Name:       " + settings.Name);
            Console.WriteLine("  Client ID:  " + (String.IsNullOrWhiteSpace(settings.ClientId) ? "(created on first connect)" : settings.ClientId));
            Console.WriteLine("  Tenant:     " + settings.TenantId);
            Console.WriteLine("  WebSocket:  " + settings.Server.Url);
            Console.WriteLine("  REST:       " + DisplayValue(settings.Server.RestUrl, "(derived from WebSocket URL)"));
            Console.WriteLine("  Auth:       " + ServerEndpoints.DescribeAuth(settings));
            Console.WriteLine("  Providers:  " + settings.Runners.Count);
            Console.WriteLine("  Colors:     " + FormatBool(settings.UseColors));
            WritePlainHeading("Connection");
            Console.WriteLine("  Connected:           " + FormatTrueFalse(connection.IsConnected));
            Console.WriteLine("  Registered:          " + FormatTrueFalse(connection.IsRegistered));
            Console.WriteLine("  Background:          " + FormatTrueFalse(connection.IsBackground));
            Console.WriteLine("  Started UTC:         " + FormatUtc(connection.StartedUtc));
            Console.WriteLine("  WebSocket UTC:       " + FormatUtc(connection.WebSocketConnectedUtc));
            Console.WriteLine("  Connected UTC:       " + FormatUtc(connection.ConnectedUtc));
            Console.WriteLine("  Registered UTC:      " + FormatUtc(connection.RegisteredUtc));
            Console.WriteLine("  Disconnected UTC:    " + FormatUtc(connection.DisconnectedUtc));
            Console.WriteLine("  Last heartbeat UTC:  " + FormatUtc(connection.LastHeartbeatUtc));
            Console.WriteLine("  Last server msg UTC: " + FormatUtc(connection.LastServerMessageUtc));
            Console.WriteLine("  Last updated UTC:    " + FormatUtc(connection.LastUpdatedUtc));
            Console.WriteLine("  Stop requested UTC:  " + FormatUtc(connection.StopRequestedUtc));
            Console.WriteLine("  Process ID:          " + (connection.ProcessId == null ? "(none)" : connection.ProcessId.Value.ToString()));
            Console.WriteLine("  Log file:            " + DisplayValue(connection.LogFilePath, ClientConnectionState.LogFile));
            Console.WriteLine("  Error log file:      " + DisplayValue(connection.ErrorLogFilePath, ClientConnectionState.ErrorLogFile));
            Console.WriteLine("  Last reason:         " + DisplayValue(connection.LastDisconnectReason, "(none)"));
            PrintLocalDashboardStatus(connection, settings);
            PrintLocalInferenceStatus(connection, settings);
            Console.WriteLine();
        }

        private static object BuildLocalDashboardJson(ClientConnectionState state)
        {
            return new
            {
                enabled = state.LocalDashboardEnabled,
                listening = state.LocalDashboardListening,
                url = state.LocalDashboardUrl ?? String.Empty,
                host = state.LocalDashboardHost ?? String.Empty,
                port = state.LocalDashboardPort ?? 0,
                startedUtc = FormatJsonUtc(state.LocalDashboardStartedUtc),
                stoppedUtc = FormatJsonUtc(state.LocalDashboardStoppedUtc),
                error = state.LocalDashboardLastError ?? String.Empty
            };
        }

        private static object BuildLocalInferenceJson(ClientConnectionState state, ClientSettings settings)
        {
            return new
            {
                enabled = state.LocalInferenceServerEnabled || settings.LocalInferenceServer.Enabled,
                listening = state.LocalInferenceServerListening,
                url = state.LocalInferenceServerUrl ?? String.Empty,
                host = state.LocalInferenceServerHost ?? settings.LocalInferenceServer.Host,
                port = state.LocalInferenceServerPort ?? settings.LocalInferenceServer.Port,
                startedUtc = FormatJsonUtc(state.LocalInferenceServerStartedUtc),
                stoppedUtc = FormatJsonUtc(state.LocalInferenceServerStoppedUtc),
                lastRequestUtc = FormatJsonUtc(state.LocalInferenceServerLastRequestUtc),
                error = state.LocalInferenceServerLastError ?? String.Empty,
                websocketAvailable = state.IsConnected && state.IsRegistered,
                routingMode = settings.LocalInferenceServer.RoutingMode.ToString(),
                routeRuleCount = settings.LocalInferenceServer.Routes.Count,
                routing = state.LocalInferenceServerListening ? LocalInferenceRoutingService.BuildView(settings, LocalInferenceRuntimeStatus.Load()) : null,
                defaultMaxConcurrentRequests = settings.DefaultMaxConcurrentRequests,
                portMigratedFrom = state.LocalInferencePortMigratedFrom,
                portMigratedTo = state.LocalInferencePortMigratedTo
            };
        }

        private static void PrintLocalDashboardAccess(ClientConnectionState state)
        {
            if (!state.LocalDashboardEnabled)
            {
                PrintStartField("Local dashboard", "disabled");
                return;
            }

            if (state.LocalDashboardListening && !String.IsNullOrWhiteSpace(state.LocalDashboardUrl))
            {
                PrintStartField("Local dashboard", state.LocalDashboardUrl);
                if (String.Equals(state.LocalDashboardHost, "0.0.0.0", StringComparison.OrdinalIgnoreCase))
                {
                    PrintStartField("Dashboard bind", "0.0.0.0 (may be reachable from other machines depending on firewall and network policy)");
                }

                return;
            }

            PrintStartField("Local dashboard", "enabled, not listening");
            if (!String.IsNullOrWhiteSpace(state.LocalDashboardLastError)) PrintStartField("Dashboard error", state.LocalDashboardLastError);
        }

        private static void PrintLocalInferenceAccess(ClientConnectionState state, ClientSettings settings)
        {
            bool enabled = state.LocalInferenceServerEnabled || settings.LocalInferenceServer.Enabled;
            if (!enabled)
            {
                PrintStartField("Local inference", "disabled");
                return;
            }

            if (state.LocalInferenceServerListening && !String.IsNullOrWhiteSpace(state.LocalInferenceServerUrl))
            {
                PrintStartField("Local inference", state.LocalInferenceServerUrl);
                return;
            }

            PrintStartField("Local inference", "enabled, not listening");
            if (!String.IsNullOrWhiteSpace(state.LocalInferenceServerLastError)) PrintStartField("Inference error", state.LocalInferenceServerLastError);
        }

        private static void PrintServerAccess(ClientSettings settings)
        {
            PrintStartField("Server WebSocket", settings.Server.Url);
            PrintStartField("Server REST", ServerEndpoints.BuildRestBaseUrl(settings));
        }

        private static void PrintStartField(string label, string value)
        {
            Console.WriteLine("  " + label.PadRight(16) + ": " + value);
        }

        private static void PrintLocalDashboardStatus(ClientConnectionState connection, ClientSettings settings)
        {
            if (!settings.LocalDashboard.Enabled && !connection.LocalDashboardEnabled)
            {
                Console.WriteLine("  Local Dashboard: disabled");
                return;
            }

            WritePlainHeading("Local Dashboard");
            Console.WriteLine("  Enabled:          " + FormatTrueFalse(settings.LocalDashboard.Enabled || connection.LocalDashboardEnabled));
            Console.WriteLine("  Listening:        " + FormatTrueFalse(connection.LocalDashboardListening));
            Console.WriteLine("  URL:              " + DisplayValue(connection.LocalDashboardUrl, "(not listening)"));
            Console.WriteLine("  Host:             " + DisplayValue(connection.LocalDashboardHost, settings.LocalDashboard.Host));
            Console.WriteLine("  Port:             " + (connection.LocalDashboardPort ?? settings.LocalDashboard.Port).ToString(CultureInfo.InvariantCulture));
            Console.WriteLine("  Started UTC:      " + FormatUtc(connection.LocalDashboardStartedUtc));
            Console.WriteLine("  Stopped UTC:      " + FormatUtc(connection.LocalDashboardStoppedUtc));
            Console.WriteLine("  Last error:       " + DisplayValue(connection.LocalDashboardLastError, "(none)"));
        }

        private static void PrintLocalInferenceStatus(ClientConnectionState connection, ClientSettings settings)
        {
            bool enabled = settings.LocalInferenceServer.Enabled || connection.LocalInferenceServerEnabled;
            WritePlainHeading("Local Inference Server");
            Console.WriteLine("  Enabled:          " + FormatTrueFalse(enabled));
            Console.WriteLine("  Listening:        " + FormatTrueFalse(connection.LocalInferenceServerListening));
            Console.WriteLine("  URL:              " + DisplayValue(connection.LocalInferenceServerUrl, "(not listening)"));
            Console.WriteLine("  Host:             " + DisplayValue(connection.LocalInferenceServerHost, settings.LocalInferenceServer.Host));
            Console.WriteLine("  Port:             " + (connection.LocalInferenceServerPort ?? settings.LocalInferenceServer.Port).ToString(CultureInfo.InvariantCulture));
            Console.WriteLine("  Started UTC:      " + FormatUtc(connection.LocalInferenceServerStartedUtc));
            Console.WriteLine("  Stopped UTC:      " + FormatUtc(connection.LocalInferenceServerStoppedUtc));
            Console.WriteLine("  Last request UTC: " + FormatUtc(connection.LocalInferenceServerLastRequestUtc));
            Console.WriteLine("  WebSocket ready:  " + FormatTrueFalse(connection.IsConnected && connection.IsRegistered));
            Console.WriteLine("  Last error:       " + DisplayValue(connection.LocalInferenceServerLastError, "(none)"));
            Console.WriteLine("  Routing:          " + settings.LocalInferenceServer.RoutingMode.ToString());
            Console.WriteLine("  Route rules:      " + settings.LocalInferenceServer.Routes.Count.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine("  Max concurrent:   " + settings.DefaultMaxConcurrentRequests.ToString(CultureInfo.InvariantCulture) + " per provider (default)");
            LocalInferenceRuntimeStatus? routingRuntime = connection.LocalInferenceServerListening ? LocalInferenceRuntimeStatus.Load() : null;
            LocalInferenceRoutingView routingView = LocalInferenceRoutingService.BuildView(settings, routingRuntime);
            if (routingRuntime != null)
            {
                Console.WriteLine("  Providers:        " + routingView.HealthyBackends.ToString(CultureInfo.InvariantCulture) + " of " + routingView.RoutableBackends.ToString(CultureInfo.InvariantCulture) + " healthy");
                foreach (LocalBackendView backend in routingView.Backends.Where(item => item.Routable))
                {
                    Console.WriteLine("    " + backend.Provider.PadRight(16) + " " + backend.Health.PadRight(10) + " " + LocalBackendRow.From(backend).Load);
                }

                Console.WriteLine("  Requests:         " + routingView.LocalRequests.ToString(CultureInfo.InvariantCulture) + " here, " + routingView.NetworkRequests.ToString(CultureInfo.InvariantCulture) + " network, " + routingView.Fallbacks.ToString(CultureInfo.InvariantCulture) + " fallbacks, " + routingView.Errors.ToString(CultureInfo.InvariantCulture) + " errors");
            }
            if (connection.LocalInferencePortMigratedFrom.HasValue)
            {
                Console.WriteLine("  Port changed:     " + connection.LocalInferencePortMigratedFrom.Value.ToString(CultureInfo.InvariantCulture) + " -> " + (connection.LocalInferencePortMigratedTo ?? settings.LocalInferenceServer.Port).ToString(CultureInfo.InvariantCulture) + " so Ollama can keep its default port. Point local apps at the new port.");
                if (!_UseJson) ClientConnectionState.AcknowledgeLocalInferencePortMigration();
            }

            if (enabled && (!connection.IsConnected || !connection.IsRegistered))
            {
                Console.WriteLine("  Failure mode:     models served on this machine keep working; requests that need the Xeno network return provider-shaped 502 responses until the websocket is registered.");
            }
        }

        private static bool TryParseProviderRoutingFlag(string[] args, ref int index, out bool? shareWithNetwork, out bool? routeLocalRequests)
        {
            shareWithNetwork = null;
            routeLocalRequests = null;
            string option = args[index];
            if (String.Equals(option, "--local-only", StringComparison.OrdinalIgnoreCase))
            {
                shareWithNetwork = false;
                return true;
            }

            if (String.Equals(option, "--share-with-network", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 < args.Length && IsBooleanLiteral(args[index + 1])) shareWithNetwork = ParseBooleanLiteral(args[++index]);
                else shareWithNetwork = true;
                return true;
            }

            if (String.Equals(option, "--local-requests", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 < args.Length && IsBooleanLiteral(args[index + 1])) routeLocalRequests = ParseBooleanLiteral(args[++index]);
                else routeLocalRequests = true;
                return true;
            }

            if (String.Equals(option, "--no-local-requests", StringComparison.OrdinalIgnoreCase))
            {
                routeLocalRequests = false;
                return true;
            }

            return false;
        }

        private static bool TryParseMaxConcurrentOption(string[] args, ref int index, out int? limit, out bool inherit)
        {
            limit = null;
            inherit = false;
            string option = args[index];
            if (index + 1 >= args.Length)
            {
                WriteError(option + " requires a value from 1 to 256, or 'inherit'.");
                return false;
            }

            string value = args[++index];
            if (String.Equals(value, "inherit", StringComparison.OrdinalIgnoreCase))
            {
                inherit = true;
                return true;
            }

            if (!Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) || parsed < 1 || parsed > 256)
            {
                WriteError(option + " requires a value from 1 to 256, or 'inherit'.");
                return false;
            }

            limit = parsed;
            return true;
        }

        private static void PrintProviders(ClientSettings settings)
        {
            PrintProviders(settings, false);
        }

        private static void PrintProviders(ClientSettings settings, bool includeModelDetails)
        {
            if (_UseJson)
            {
                WriteJson(settings.Runners.ConvertAll(runner => new
                {
                    name = runner.Name,
                    apiType = runner.ApiType,
                    endpoint = runner.Endpoint,
                    timeoutMs = runner.TimeoutMs,
                    allowModelManagement = ProviderConfiguration.IsModelManagementAllowed(runner),
                    modelManagementAllowed = ProviderConfiguration.IsModelManagementAllowed(runner),
                    credentialConfigured = !String.IsNullOrWhiteSpace(runner.Credential),
                    shareWithNetwork = runner.ShareWithNetwork,
                    routeLocalRequests = runner.RouteLocalRequests,
                    maxConcurrentRequests = runner.MaxConcurrentRequests,
                    effectiveMaxConcurrentRequests = ConcurrencyLimitResolver.Resolve(settings, runner, null).ProviderLimit,
                    models = runner.Models.ConvertAll(model => new
                    {
                        name = model.Name,
                        supportsCompletions = model.SupportsCompletions,
                        supportsEmbeddings = model.SupportsEmbeddings,
                        maxConcurrentRequests = model.MaxConcurrentRequests,
                        effectiveMaxConcurrentRequests = ConcurrencyLimitResolver.Resolve(settings, runner, model.Name).EffectiveModelLimit
                    })
                }));
                return;
            }

            if (settings.Runners.Count == 0)
            {
                WriteInfoHeading("No provider endpoints are configured.");
                return;
            }

            WriteInfoHeading("Configured provider endpoints");
            for (int i = 0; i < settings.Runners.Count; i++)
            {
                if (i > 0) Console.WriteLine();
                ClientRunnerSettings runner = settings.Runners[i];
                Console.WriteLine("  - " + runner.Name);
                Console.WriteLine("    API Type:   " + runner.ApiType);
                Console.WriteLine("    Endpoint:   " + runner.Endpoint);
                Console.WriteLine("    Timeout:    " + runner.TimeoutMs + " ms");
                Console.WriteLine("    Credential: " + (String.IsNullOrWhiteSpace(runner.Credential) ? "not configured" : "configured"));
                Console.WriteLine("    Model management allowed: " + FormatBool(ProviderConfiguration.IsModelManagementAllowed(runner)));
                ConcurrencyLimits providerLimits = ConcurrencyLimitResolver.Resolve(settings, runner, null);
                Console.WriteLine("    Shared with network: " + FormatBool(runner.ShareWithNetwork));
                Console.WriteLine("    Local requests:      " + FormatBool(runner.RouteLocalRequests));
                Console.WriteLine("    Max concurrent:      " + providerLimits.ProviderLimit.ToString(CultureInfo.InvariantCulture) + " (" + providerLimits.ProviderLimitSource + ")");
                Console.WriteLine("    Models:     " + runner.Models.Count);
                if (includeModelDetails && runner.Models.Count > 0)
                {
                    foreach (ClientModelSettings model in runner.Models)
                    {
                        Console.WriteLine("      - " + model.Name);
                        Console.WriteLine("        Completions: " + FormatBool(model.SupportsCompletions));
                        Console.WriteLine("        Embeddings:  " + FormatBool(model.SupportsEmbeddings));
                        if (model.MaxConcurrentRequests.HasValue)
                        {
                            ConcurrencyLimits modelLimits = ConcurrencyLimitResolver.Resolve(settings, runner, model.Name);
                            Console.WriteLine("        Max concurrent: " + modelLimits.EffectiveModelLimit.ToString(CultureInfo.InvariantCulture) + (modelLimits.ModelLimitExceedsProvider ? " (model limit " + model.MaxConcurrentRequests.Value.ToString(CultureInfo.InvariantCulture) + " is above the provider limit)" : String.Empty));
                        }
                    }
                }
            }
        }

        private static void PrintModels(ClientSettings settings)
        {
            if (_UseJson)
            {
                List<object> models = new List<object>();
                foreach (ClientRunnerSettings runner in settings.Runners)
                {
                    foreach (ClientModelSettings model in runner.Models)
                    {
                        models.Add(new
                        {
                            name = model.Name,
                            providerName = runner.Name,
                            apiType = runner.ApiType,
                            modelManagementAllowed = ProviderConfiguration.IsModelManagementAllowed(runner),
                            supportsCompletions = model.SupportsCompletions,
                            supportsEmbeddings = model.SupportsEmbeddings
                        });
                    }
                }

                WriteJson(models);
                return;
            }

            bool any = false;
            foreach (ClientRunnerSettings runner in settings.Runners)
            {
                foreach (ClientModelSettings model in runner.Models)
                {
                    if (!any) WriteInfoHeading("Configured models");
                    else Console.WriteLine();
                    any = true;
                    Console.WriteLine("  - " + model.Name);
                    Console.WriteLine("    Provider:    " + runner.Name);
                    Console.WriteLine("    API Type:    " + runner.ApiType);
                    Console.WriteLine("    Completions: " + FormatBool(model.SupportsCompletions));
                    Console.WriteLine("    Embeddings:  " + FormatBool(model.SupportsEmbeddings));
                }
            }

            if (!any) WriteInfoHeading("No models are configured.");
        }

        private static string DisplayValue(string? value, string emptyText)
        {
            return String.IsNullOrWhiteSpace(value) ? emptyText : value;
        }

        private static string MaskSecret(string? value)
        {
            return ServerEndpoints.MaskSecret(value);
        }

        private static string FormatBool(bool value)
        {
            return value ? "yes" : "no";
        }

        private static string FormatTrueFalse(bool value)
        {
            return value ? "true" : "false";
        }

        private static string FormatUtc(DateTime? value)
        {
            return value == null ? "(never)" : value.Value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss 'UTC'");
        }

        private static string FormatJsonUtc(DateTime? value)
        {
            return value == null ? String.Empty : value.Value.ToUniversalTime().ToString("O");
        }

        private static string FormatPingTime(long elapsedMs)
        {
            return elapsedMs <= 0 ? "<1ms" : elapsedMs.ToString(CultureInfo.InvariantCulture) + "ms";
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes <= 0) return "unknown";
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double value = bytes;
            int unit = 0;
            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }

            return value.ToString("0.##") + " " + units[unit];
        }

        private static bool IsHelp(string value)
        {
            return String.Equals(value, "help", StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(value, "--help", StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(value, "-h", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsListCommand(string value)
        {
            return String.Equals(value, "list", StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(value, "ls", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsDeleteCommand(string value)
        {
            return String.Equals(value, "delete", StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(value, "remove", StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(value, "remote", StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(value, "rm", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsUpdateCommand(string value)
        {
            return String.Equals(value, "update", StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(value, "edit", StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(value, "set", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsBooleanLiteral(string value)
        {
            return String.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(value, "false", StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(value, "no", StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(value, "on", StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(value, "off", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ParseBooleanLiteral(string value)
        {
            return String.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
        }

        private static void PrintConfigHelp()
        {
            WriteInfoHeading("Configuration commands");
            Console.WriteLine("  xeno config show");
            Console.WriteLine("      Show the client configuration, including local dashboard and local inference settings when enabled.");
            Console.WriteLine("  xeno config colors on");
            Console.WriteLine("      Enable colored command output.");
            Console.WriteLine("  xeno config colors off");
            Console.WriteLine("      Disable colored command output.");
            Console.WriteLine();
        }

        private static void PrintServerHelp()
        {
            WriteInfoHeading("Server commands");
            Console.WriteLine("  xeno server show");
            Console.WriteLine("      Show the Xeno server this client points at.");
            Console.WriteLine("  xeno server use <cloud|url|host[:port]>");
            Console.WriteLine("      Point this client at a Xeno server. cloud is the Xeno cloud server (the default);");
            Console.WriteLine("      http(s)://host:port or ws(s)://host:port also work, and host[:port] uses port 9000.");
            Console.WriteLine("  xeno server set-url <websocket-url>");
            Console.WriteLine("      Set the Xeno server WebSocket endpoint.");
            Console.WriteLine("  xeno server set-rest-url <rest-url>");
            Console.WriteLine("      Set the Xeno server REST API endpoint.");
            Console.WriteLine();
        }

        private static void PrintResetHelp()
        {
            WriteInfoHeading("Reset command");
            Console.WriteLine("  xeno reset");
            Console.WriteLine("      Prompt for confirmation, then restore ~/.xeno/client.json to factory defaults and remove runtime state.");
            Console.WriteLine("  xeno reset --force");
            Console.WriteLine("      Restore factory defaults without prompting.");
            Console.WriteLine();
        }

        private static void PrintLoginHelp()
        {
            WriteInfoHeading("Login command");
            Console.WriteLine("  xeno login -u <username>");
            Console.WriteLine("      Prompt for a password and store only the returned client credential.");
            Console.WriteLine("  xeno login -u <username> --tenant <tenant-id> --server <rest-url>");
            Console.WriteLine("      Login against a specific tenant and REST server.");
            Console.WriteLine();
        }

        private static void PrintAuthHelp()
        {
            WriteInfoHeading("Authentication commands");
            Console.WriteLine("  xeno login -u <username> [--tenant <tenant-id>] [--server <rest-url>]");
            Console.WriteLine("      Prompt for a password and store only the returned access key.");
            Console.WriteLine("  xeno set-auth --access-key <key>");
            Console.WriteLine("      Store an existing client credential for WebSocket and API use.");
            Console.WriteLine("  xeno set-auth --clear");
            Console.WriteLine("      Remove stored authentication.");
            Console.WriteLine();
        }

        private static void PrintProviderHelp()
        {
            WriteInfoHeading("Provider commands");
            Console.WriteLine("  xeno provider list");
            Console.WriteLine("  xeno provider ls");
            Console.WriteLine("      Show configured provider endpoints.");
            Console.WriteLine("  xeno provider test --name <name>");
            Console.WriteLine("      Test connectivity to one configured provider endpoint.");
            Console.WriteLine("  xeno provider delete --name <name>");
            Console.WriteLine("      Aliases: remove, remote, rm.");
            Console.WriteLine("      Delete one configured provider endpoint.");
            Console.WriteLine("  xeno provider add --name <name> --type <api-type> --endpoint <url> [--credential <value>] [--allow-model-management]");
            Console.WriteLine("      Add an Ollama, OpenAI, Gemini, or compatible endpoint.");
            Console.WriteLine("      --allow-model-management permits dashboard pull/delete commands for Ollama providers.");
            Console.WriteLine("  xeno provider update --name <name> [--new-name <name>] [--type <api-type>] [--endpoint <url>] [--timeout <ms>] [--credential <value>|--clear-credential] [--allow-model-management|--no-model-management]");
            Console.WriteLine("      Update a configured provider endpoint and request inventory re-advertisement when connected.");
            Console.WriteLine("  Routing and concurrency options (add and update):");
            Console.WriteLine("      --local-only                     Keep the provider off the Xeno network; this machine's apps can still use it.");
            Console.WriteLine("      --share-with-network [true|false] Offer the provider's models to the Xeno network (default true).");
            Console.WriteLine("      --local-requests [true|false]    Answer this machine's local endpoint requests with this provider (default true).");
            Console.WriteLine("      --no-local-requests              Same as --local-requests false.");
            Console.WriteLine("      --max-concurrent <1-256|inherit> Limit across the provider's models; inherit uses the global default.");
            Console.WriteLine();
        }

        private static void PrintModelHelp()
        {
            WriteInfoHeading("Model commands");
            Console.WriteLine("  xeno model list");
            Console.WriteLine("  xeno model ls");
            Console.WriteLine("      Show models advertised by this client.");
            Console.WriteLine("  xeno model add --provider <provider-name> --name <model-name> [--type completion|embedding|both]");
            Console.WriteLine("  xeno model add --provider <provider-name> --name <model-name> [--completions true|false] [--embeddings true|false]");
            Console.WriteLine("      Add a model to a configured provider.");
            Console.WriteLine("  xeno model update --name <model-name> [--provider <provider-name>] [--new-name <model-name>] [--new-provider <provider-name>] [--type completion|embedding|both]");
            Console.WriteLine("      Update a model advertisement and request inventory re-advertisement when connected.");
            Console.WriteLine("      --max-concurrent <1-256|inherit> (add and update) limits this model on its provider; inherit uses the provider limit.");
            Console.WriteLine("  xeno model delete --name <model-name> [--provider <provider-name>]");
            Console.WriteLine("      Aliases: remove, remote, rm.");
            Console.WriteLine("      Delete a model from one provider, or from all providers when omitted.");
            Console.WriteLine();
        }

        private static void PrintConnectHelp()
        {
            WriteInfoHeading("Connect commands");
            Console.WriteLine("  xeno connect");
            Console.WriteLine("  xeno start");
            Console.WriteLine("      Start the client as a background process and return after registration succeeds. Prints local dashboard and local inference URLs when enabled.");
            Console.WriteLine("  xeno restart [--force] [--timeout <milliseconds>]");
            Console.WriteLine("      Stop the background client process, then start it again.");
            Console.WriteLine("  xeno connect --foreground");
            Console.WriteLine("      Run the client WebSocket loop in the current terminal. Prints local dashboard and local inference URLs when enabled.");
            Console.WriteLine("  xeno connect --timeout <milliseconds>");
            Console.WriteLine("      Wait this long for background registration before returning an error.");
            Console.WriteLine();
        }

        private static void PrintRestartHelp()
        {
            WriteInfoHeading("Restart command");
            Console.WriteLine("  xeno restart");
            Console.WriteLine("      Run 'xeno stop' followed by 'xeno start' as one command.");
            Console.WriteLine("  xeno restart --force");
            Console.WriteLine("      Request stop, then terminate the recorded process if it does not exit before starting again.");
            Console.WriteLine("  xeno restart --timeout <milliseconds>");
            Console.WriteLine("      Wait this long for background registration after restarting before returning an error.");
            Console.WriteLine();
        }

        private static void PrintLogsHelp()
        {
            WriteInfoHeading("Log commands");
            Console.WriteLine("  xeno logs");
            Console.WriteLine("      Show the most recent Xeno client log lines.");
            Console.WriteLine("  xeno logs -f");
            Console.WriteLine("      Follow the Xeno client log file as new lines are written.");
            Console.WriteLine("  xeno logs --tail <lines>");
            Console.WriteLine("      Control how many previous lines are shown.");
            Console.WriteLine("  xeno logs --errors");
            Console.WriteLine("      Read the error log instead of the standard client log.");
            Console.WriteLine();
        }

        private static void PrintPingHelp()
        {
            WriteInfoHeading("Ping command");
            Console.WriteLine("  xeno ping");
            Console.WriteLine("      Send 5 real-time HTTP heartbeat probes to the configured Xeno server.");
            Console.WriteLine("  xeno ping -n <count>");
            Console.WriteLine("  xeno ping --count <count>");
            Console.WriteLine("      Send a specific number of probes. Valid range: 1 through 1000.");
            Console.WriteLine("  xeno ping -w <milliseconds>");
            Console.WriteLine("  xeno ping --timeout <milliseconds>");
            Console.WriteLine("      Set the per-probe timeout. Valid range: 100 through 600000.");
            Console.WriteLine("  xeno ping --server <rest-url>");
            Console.WriteLine("      Probe a REST server URL without saving it to client settings.");
            Console.WriteLine();
        }

        private static void PrintDashboardHelp()
        {
            WriteInfoHeading("Dashboard commands");
            Console.WriteLine("  xeno dashboard");
            Console.WriteLine("      Show the local dashboard URL when LocalDashboard.Enabled is true and the worker is listening.");
            Console.WriteLine("  xeno dashboard open");
            Console.WriteLine("      Open the local dashboard in the default browser.");
            Console.WriteLine();
        }

        private static void PrintReloadHelp()
        {
            WriteInfoHeading("Reload command");
            Console.WriteLine("  xeno reload");
            Console.WriteLine("      Reload ~/.xeno/client.json from disk and request provider/model inventory re-advertisement when connected.");
            Console.WriteLine();
        }

        private static void PrintStopHelp()
        {
            WriteInfoHeading("Stop commands");
            Console.WriteLine("  xeno stop");
            Console.WriteLine("  xeno disconnect");
            Console.WriteLine("      Request the background Xeno client process to stop.");
            Console.WriteLine("  xeno stop --force");
            Console.WriteLine("      Request stop, then terminate the recorded process if it does not exit.");
            Console.WriteLine();
        }

        private static void WriteSuccess(string message)
        {
            WriteColored(message, ConsoleColor.Green);
        }

        private static void WriteInfo(string message)
        {
            WriteColored(message, ConsoleColor.Cyan);
        }

        private static void WriteDebug(string message)
        {
            if (_WorkerOutputLogWriter != null)
            {
                _WorkerOutputLogWriter.WriteLineWithSeverity("DEBUG", message);
                return;
            }

            WriteColored(message, ConsoleColor.DarkGray);
        }

        private static void WriteWarning(string message)
        {
            WriteColored(message, ConsoleColor.Yellow);
        }

        private static void WriteError(string message)
        {
            // Errors go to stderr so a script reading stdout, for example with --json, never parses them as output.
            if (!_UseColors || Console.IsErrorRedirected)
            {
                Console.Error.WriteLine("Error: " + message);
                return;
            }

            ConsoleColor previous = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine("Error: " + message);
            Console.ForegroundColor = previous;
        }

        private static void WriteInfoHeading(string message)
        {
            Console.WriteLine();
            WriteInfo(message);
            Console.WriteLine();
        }

        private static void WriteSuccessHeading(string message)
        {
            Console.WriteLine();
            WriteSuccess(message);
            Console.WriteLine();
        }

        private static void WriteWarningHeading(string message)
        {
            Console.WriteLine();
            WriteWarning(message);
            Console.WriteLine();
        }

        private static void WritePlainHeading(string message)
        {
            Console.WriteLine();
            Console.WriteLine(message);
            Console.WriteLine();
        }

        private static void WriteColored(string message, ConsoleColor color)
        {
            if (!_UseColors || Console.IsOutputRedirected)
            {
                Console.WriteLine(message);
                return;
            }

            ConsoleColor previous = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ForegroundColor = previous;
        }

        private static void PrintHelp()
        {
            WriteInfoHeading("xeno " + ClientVersion);
            Console.WriteLine("Usage:");
            Console.WriteLine();
            Console.WriteLine("  xeno <command> [options]");
            WritePlainHeading("Common commands:");
            Console.WriteLine("  xeno init");
            Console.WriteLine("  xeno reset");
            Console.WriteLine("  xeno status");
            Console.WriteLine("  xeno ping");
            Console.WriteLine("  xeno inventory");
            Console.WriteLine("  xeno reload");
            Console.WriteLine("  xeno connect");
            Console.WriteLine("  xeno start");
            Console.WriteLine("  xeno restart");
            Console.WriteLine("  xeno connect --foreground");
            Console.WriteLine("  xeno logs -f");
            Console.WriteLine("  xeno dashboard");
            Console.WriteLine("  xeno dashboard open");
            Console.WriteLine("  xeno stop");
            Console.WriteLine("  xeno disconnect");
            WritePlainHeading("Chat:");
            Console.WriteLine("  xeno chat                Chat with a model on this machine or the Xeno network");
            Console.WriteLine("  xeno chat -m <model> \"message\"   Ask once");
            Console.WriteLine("  xeno chat list | show | delete | rate | history | feedback   (xeno chat --help)");
            WritePlainHeading("Usage:");
            Console.WriteLine("  xeno usage               Latency, tokens, groups, and stage timing for served requests");
            Console.WriteLine("  xeno usage --dataset endpoint|chat --range hour|day|week|month [--group-by model|provider|route] [--json]");
            WritePlainHeading("MCP:");
            Console.WriteLine("  xeno mcp                 Serve Xeno's tools over stdio (for Claude Code and other MCP clients)");
            Console.WriteLine("  xeno mcp tools           List the tools");
            Console.WriteLine("  xeno mcp config          Show how to add Xeno to an MCP client");
            Console.WriteLine("  xeno mcp token [--rotate]  Show (or replace) the token for the HTTP MCP server");
            WritePlainHeading("Configuration:");
            Console.WriteLine("  xeno config show");
            Console.WriteLine("  xeno config colors on");
            Console.WriteLine("  xeno config colors off");
            Console.WriteLine("  xeno server show");
            Console.WriteLine("  xeno server use <cloud|url|host[:port]>");
            Console.WriteLine("  xeno server set-url <websocket-url>");
            Console.WriteLine("  xeno server set-rest-url <rest-url>");
            WritePlainHeading("Authentication:");
            Console.WriteLine("  xeno login -u <username> [--tenant <tenant-id>] [--server <rest-url>]");
            Console.WriteLine("  xeno set-auth --access-key <key>");
            Console.WriteLine("  xeno set-auth --clear");
            WritePlainHeading("Providers:");
            Console.WriteLine("  xeno provider list");
            Console.WriteLine("  xeno provider ls");
            Console.WriteLine("  xeno provider test --name <name>");
            Console.WriteLine("  xeno provider delete --name <name>  (aliases: remove, remote, rm)");
            Console.WriteLine("  xeno provider add --name <name> --type <api-type> --endpoint <url> [--credential <value>] [--allow-model-management]");
            Console.WriteLine("  xeno provider update --name <name> [--new-name <name>] [--type <api-type>] [--endpoint <url>] [--timeout <ms>] [--credential <value>|--clear-credential] [--allow-model-management|--no-model-management]");
            WritePlainHeading("Models:");
            Console.WriteLine("  xeno model list");
            Console.WriteLine("  xeno model ls");
            Console.WriteLine("  xeno model add --provider <provider-name> --name <model-name> [--type completion|embedding|both]");
            Console.WriteLine("  xeno model update --name <model-name> [--provider <provider-name>] [--new-name <model-name>] [--new-provider <provider-name>] [--type completion|embedding|both]");
            Console.WriteLine("  xeno model delete --name <model-name> [--provider <provider-name>]  (aliases: remove, remote, rm)");
            Console.WriteLine();
            Console.WriteLine("  xeno inference routes | resolve <model> | route set|remove <model> | backends [--check] | concurrency <n> | requests");
            Console.WriteLine("      Local endpoint routing: where each model goes, provider health and load, and recent requests.");
            WritePlainHeading("Runtime:");
            Console.WriteLine("  xeno reload");
            Console.WriteLine("  xeno connect");
            Console.WriteLine("  xeno start");
            Console.WriteLine("  xeno restart");
            Console.WriteLine("  xeno connect --foreground");
            Console.WriteLine("  xeno logs [--tail <lines>] [-f]");
            Console.WriteLine("  xeno ping [-n <count>] [-w <milliseconds>] [--server <rest-url>]");
            Console.WriteLine("  xeno dashboard [open]");
            Console.WriteLine("  xeno stop [--force]");
            Console.WriteLine("  xeno disconnect");
            Console.WriteLine();
            Console.WriteLine("When LocalDashboard.Enabled is true, 'xeno connect', 'xeno status', and 'xeno dashboard' show the local dashboard URL.");
            Console.WriteLine("When LocalInferenceServer.Enabled is true, 'xeno connect' and 'xeno status' show the local OpenAI/Ollama-compatible listener URL, its routing, and provider health.");
            WritePlainHeading("Apps:");
            Console.WriteLine("  xeno tui [--page <page>] [--server <url>] [--home <dir>]");
            Console.WriteLine("      Terminal app with every client page and action.");
            Console.WriteLine("  xeno-desktop [--page <page>] [--server <url>] [--home <dir>]");
            Console.WriteLine("      Desktop app (installed beside xeno).");
            Console.WriteLine("  xeno-tray");
            Console.WriteLine("      Xeno Agent tray app: status, start/stop, and launchers.");
            WritePlainHeading("Global options:");
            Console.WriteLine("  --no-color    Disable colored output for this command.");
            Console.WriteLine("  --color       Enable colored output for this command.");
            Console.WriteLine("  --json        Emit machine-readable JSON for supported commands.");
            WritePlainHeading("Reset:");
            Console.WriteLine("  xeno reset");
            Console.WriteLine("  xeno reset --force");
            Console.WriteLine();
            Console.WriteLine("Use 'xeno login' when you have a username/password. Use 'xeno set-auth' when you already have an access key.");
            Console.WriteLine();
        }
    }
}

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
    using Xeno.Client.LocalDashboard;
    using Xeno.Client.Telemetry.Providers;
    using Xeno.Client.Telemetry.Services;
    using Xeno.Core.Enums;
    using Xeno.Core.Helpers;
    using Xeno.Core.Models;
    using Xeno.Core.Models.Telemetry;
    using Xeno.Core.Serialization;

    /// <summary>
    /// Xeno client CLI entry point.
    /// </summary>
    public static class Program
    {
        private const string ClientVersion = "0.1.0";
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

        /// <summary>
        /// Run the Xeno client CLI.
        /// </summary>
        /// <param name="args">Arguments.</param>
        /// <returns>Exit code.</returns>
        public static async Task<int> Main(string[] args)
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
                await RunForegroundConnectAsync(settings, worker, CancellationToken.None).ConfigureAwait(false);
                return 0;
            }

            return await StartBackgroundConnectAsync(settings, timeoutMs, CancellationToken.None).ConfigureAwait(false);
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

            await PrintLogTailAsync(logFile, tailLines, token).ConfigureAwait(false);
            if (follow) await FollowLogAsync(logFile, token).ConfigureAwait(false);
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

            Uri endpoint = BuildRestUri(pingSettings, "/v1.0/api/ping");
            string target = endpoint.Host;
            List<long> responseTimes = new List<long>();
            List<object> attempts = new List<object>();
            int received = 0;

            if (!_UseJson)
            {
                Console.WriteLine("Pinging " + target + " [" + endpoint.GetLeftPart(UriPartial.Authority) + "] with HTTP heartbeat:");
                Console.WriteLine();
            }

            using (HttpClient httpClient = new HttpClient())
            {
                for (int sequence = 1; sequence <= count; sequence++)
                {
                    DateTime clientSentUtc = DateTime.UtcNow;
                    Uri requestUri = BuildPingUri(endpoint, sequence, clientSentUtc);
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    try
                    {
                        using (CancellationTokenSource timeout = new CancellationTokenSource(timeoutMs))
                        using (HttpResponseMessage response = await httpClient.GetAsync(requestUri, timeout.Token).ConfigureAwait(false))
                        {
                            string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                            stopwatch.Stop();
                            long elapsedMs = stopwatch.ElapsedMilliseconds;
                            int bytes = Encoding.UTF8.GetByteCount(responseBody);
                            string status = TryReadJsonProperty(responseBody, "status");
                            string serverUtc = TryReadJsonProperty(responseBody, "serverUtc");
                            bool success = response.IsSuccessStatusCode;
                            if (success)
                            {
                                received++;
                                responseTimes.Add(elapsedMs);
                                if (!_UseJson)
                                {
                                    Console.WriteLine("Reply from " + target + ": bytes=" + bytes.ToString(CultureInfo.InvariantCulture) + " time=" + FormatPingTime(elapsedMs) + " seq=" + sequence.ToString(CultureInfo.InvariantCulture) + " status=" + DisplayValue(status, "ok") + " serverUtc=" + DisplayValue(serverUtc, "unknown"));
                                }
                            }
                            else if (!_UseJson)
                            {
                                Console.WriteLine("Reply from " + target + ": HTTP " + ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture) + " " + response.ReasonPhrase + " time=" + FormatPingTime(elapsedMs));
                            }

                            attempts.Add(new
                            {
                                sequence,
                                success,
                                statusCode = (int)response.StatusCode,
                                bytes,
                                elapsedMs,
                                status,
                                serverUtc
                            });
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        stopwatch.Stop();
                        if (!_UseJson) Console.WriteLine("Request timed out.");
                        attempts.Add(new
                        {
                            sequence,
                            success = false,
                            statusCode = 0,
                            bytes = 0,
                            elapsedMs = timeoutMs,
                            status = String.Empty,
                            serverUtc = String.Empty,
                            error = "Timeout"
                        });
                    }
                    catch (HttpRequestException ex)
                    {
                        stopwatch.Stop();
                        if (!_UseJson) Console.WriteLine("Request failed: " + ex.Message);
                        attempts.Add(new
                        {
                            sequence,
                            success = false,
                            statusCode = 0,
                            bytes = 0,
                            elapsedMs = stopwatch.ElapsedMilliseconds,
                            status = String.Empty,
                            serverUtc = String.Empty,
                            error = ex.Message
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

            ClientConnectionState state = ClientConnectionState.Load();
            if (state.ProcessId == null || !state.IsRecordedProcessRunning())
            {
                if (_UseJson) WriteJson(new { stopped = true, reason = "not-running" });
                else WriteInfo("Xeno client is not running.");
                return 0;
            }

            ClientConnectionState.RequestStop();
            bool stopped = await WaitForProcessExitAsync(state.ProcessId.Value, DefaultStopTimeoutMs).ConfigureAwait(false);
            if (!stopped && force)
            {
                stopped = KillProcess(state.ProcessId.Value);
            }

            if (_UseJson)
            {
                WriteJson(new
                {
                    stopped,
                    processId = state.ProcessId.Value,
                    force
                });
                return stopped ? 0 : 1;
            }

            if (stopped)
            {
                WriteSuccess("Stopped Xeno client process " + state.ProcessId.Value + ".");
                return 0;
            }

            WriteWarning("Stop was requested, but process " + state.ProcessId.Value + " is still running. Use 'xeno stop --force' to terminate it.");
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
                if (!String.IsNullOrWhiteSpace(state.LocalDashboardLastError)) Console.WriteLine("  Last error: " + state.LocalDashboardLastError);
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
            string clientId = EnsureClientId(reloaded);
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

            if (!String.IsNullOrWhiteSpace(serverUrl))
            {
                settings.Server.RestUrl = serverUrl;
            }

            string password = ReadSecret("Password: ");
            if (String.IsNullOrWhiteSpace(password))
            {
                WriteError("Password is required.");
                return 1;
            }

            string clientId = EnsureClientId(settings);
            Uri endpoint = BuildRestUri(settings, "/v1.0/api/auth/login");
            using (HttpClient httpClient = new HttpClient())
            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, endpoint))
            {
                request.Headers.Add("x-tenant-guid", tenantId);
                request.Content = new StringContent(JsonUtil.Serialize(new
                {
                    username,
                    password,
                    tenantId,
                    clientId,
                    label = settings.Name
                }), Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await httpClient.SendAsync(request).ConfigureAwait(false))
                {
                    string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        WriteError("Login failed: " + FormatHttpError(response, responseBody));
                        return 1;
                    }

                    LoginCredentialResponse? credential = JsonUtil.Deserialize<LoginCredentialResponse>(responseBody);
                    string? accessKey = credential?.AccessKey;
                    string? returnedTenantId = credential?.TenantId;
                    if (String.IsNullOrWhiteSpace(accessKey))
                    {
                        WriteError("Login response did not include a client credential.");
                        return 1;
                    }

                    settings.TenantId = String.IsNullOrWhiteSpace(returnedTenantId) ? tenantId : returnedTenantId;
                    settings.Server.AccessKey = accessKey;
                    settings.Server.SecretKey = null;
                    settings.Server.Token = null;
                    settings.Save();
                }
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

                ClientRunnerSettings? runner = settings.Runners.Find(item => String.Equals(item.Name, runnerName, StringComparison.OrdinalIgnoreCase));
                if (runner == null)
                {
                    WriteError("Provider endpoint not found: " + runnerName);
                    return 1;
                }

                bool success = await TestRunnerAsync(runner, CancellationToken.None).ConfigureAwait(false);
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

                int removed = settings.Runners.RemoveAll(item => String.Equals(item.Name, runnerName, StringComparison.OrdinalIgnoreCase));
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
                int? timeoutMs = null;
                for (int i = 2; i < args.Length; i++)
                {
                    string option = args[i];
                    if (String.Equals(option, "--clear-credential", StringComparison.OrdinalIgnoreCase))
                    {
                        clearCredential = true;
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

                ClientRunnerSettings? runner = settings.Runners.Find(item => String.Equals(item.Name, runnerName, StringComparison.OrdinalIgnoreCase));
                if (runner == null)
                {
                    WriteError("Provider endpoint not found: " + runnerName);
                    return 1;
                }

                if (!String.IsNullOrWhiteSpace(newName) && !String.Equals(newName, runner.Name, StringComparison.OrdinalIgnoreCase) &&
                    settings.Runners.Exists(item => String.Equals(item.Name, newName, StringComparison.OrdinalIgnoreCase)))
                {
                    WriteError("Provider endpoint already exists: " + newName);
                    return 1;
                }

                if (!String.IsNullOrWhiteSpace(newName)) runner.Name = newName;
                if (!String.IsNullOrWhiteSpace(apiType)) runner.ApiType = apiType;
                if (!String.IsNullOrWhiteSpace(endpoint)) runner.Endpoint = endpoint;
                if (timeoutMs != null) runner.TimeoutMs = timeoutMs.Value;
                if (clearCredential) runner.Credential = null;
                else if (credential != null) runner.Credential = credential;

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

                if (usedLegacyProviderOption) WriteWarning("--provider is deprecated for 'xeno provider add'; use --type for the upstream API type.");

                ClientRunnerSettings? duplicate = FindDuplicateProvider(settings, runner);
                if (duplicate != null)
                {
                    if (String.Equals(duplicate.Name, runner.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        WriteInfo("Provider endpoint '" + duplicate.Name + "' already exists; no changes made.");
                    }
                    else
                    {
                        WriteInfo("Provider endpoint '" + duplicate.Name + "' already uses " + duplicate.Endpoint + "; no changes made.");
                    }

                    return 0;
                }

                settings.Runners.Add(runner);
                SaveInventorySettings(settings);
                WriteSuccess("Added provider endpoint '" + runner.Name + "'.");
                return 0;
            }

            WriteError("Unknown provider command.");
            PrintProviderHelp();
            return 1;
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
                for (int i = 2; i < args.Length; i++)
                {
                    string option = args[i];
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

                ClientRunnerSettings? runner = FindModelAddProvider(settings, runnerName);
                if (runner == null)
                {
                    WriteError("Provider endpoint not found: " + runnerName);
                    return 1;
                }

                if (!supportsCompletions && !supportsEmbeddings)
                {
                    WriteError("Model must support completions, embeddings, or both.");
                    PrintModelHelp();
                    return 1;
                }

                runner.Models.Add(new ClientModelSettings
                {
                    Name = modelName,
                    SupportsCompletions = supportsCompletions,
                    SupportsEmbeddings = supportsEmbeddings
                });
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
                for (int i = 2; i < args.Length; i++)
                {
                    string option = args[i];
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

                ClientRunnerSettings? sourceRunner = null;
                ClientModelSettings? model = null;
                int matches = 0;
                foreach (ClientRunnerSettings runner in settings.Runners)
                {
                    if (!String.IsNullOrWhiteSpace(runnerName) && !String.Equals(runner.Name, runnerName, StringComparison.OrdinalIgnoreCase)) continue;
                    foreach (ClientModelSettings candidate in runner.Models)
                    {
                        if (!String.Equals(candidate.Name, modelName, StringComparison.OrdinalIgnoreCase)) continue;
                        sourceRunner = runner;
                        model = candidate;
                        matches++;
                    }
                }

                if (matches == 0)
                {
                    WriteError("Model not found: " + modelName);
                    return 1;
                }

                if (matches > 1 && String.IsNullOrWhiteSpace(runnerName))
                {
                    WriteError("Model name exists in multiple providers. Specify --provider.");
                    return 1;
                }

                if (sourceRunner == null || model == null)
                {
                    WriteError("Model not found: " + modelName);
                    return 1;
                }

                ClientRunnerSettings targetRunner = sourceRunner;
                if (!String.IsNullOrWhiteSpace(newRunnerName))
                {
                    ClientRunnerSettings? foundRunner = settings.Runners.Find(item => String.Equals(item.Name, newRunnerName, StringComparison.OrdinalIgnoreCase));
                    if (foundRunner == null)
                    {
                        WriteError("Provider endpoint not found: " + newRunnerName);
                        return 1;
                    }

                    targetRunner = foundRunner;
                }

                string targetModelName = String.IsNullOrWhiteSpace(newModelName) ? model.Name : newModelName;
                bool moving = !Object.ReferenceEquals(sourceRunner, targetRunner);
                bool renaming = !String.Equals(model.Name, targetModelName, StringComparison.OrdinalIgnoreCase);
                if ((moving || renaming) && targetRunner.Models.Exists(item => String.Equals(item.Name, targetModelName, StringComparison.OrdinalIgnoreCase)))
                {
                    WriteError("Model already exists in provider endpoint '" + targetRunner.Name + "': " + targetModelName);
                    return 1;
                }

                bool finalSupportsCompletions = supportsCompletions ?? model.SupportsCompletions;
                bool finalSupportsEmbeddings = supportsEmbeddings ?? model.SupportsEmbeddings;
                if (!finalSupportsCompletions && !finalSupportsEmbeddings)
                {
                    WriteError("Model must support completions, embeddings, or both.");
                    PrintModelHelp();
                    return 1;
                }

                if (moving)
                {
                    sourceRunner.Models.Remove(model);
                    targetRunner.Models.Add(model);
                }

                model.Name = targetModelName;
                model.SupportsCompletions = finalSupportsCompletions;
                model.SupportsEmbeddings = finalSupportsEmbeddings;
                SaveInventorySettings(settings);
                WriteSuccess("Updated model '" + model.Name + "' in provider endpoint '" + targetRunner.Name + "'.");
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

                if (!String.IsNullOrWhiteSpace(runnerName) && !settings.Runners.Exists(item => String.Equals(item.Name, runnerName, StringComparison.OrdinalIgnoreCase)))
                {
                    WriteError("Provider endpoint not found: " + runnerName);
                    return 1;
                }

                int removed = 0;
                foreach (ClientRunnerSettings runner in settings.Runners)
                {
                    if (!String.IsNullOrWhiteSpace(runnerName) && !String.Equals(runner.Name, runnerName, StringComparison.OrdinalIgnoreCase)) continue;
                    removed += runner.Models.RemoveAll(item => String.Equals(item.Name, modelName, StringComparison.OrdinalIgnoreCase));
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
            EnsureClientId(settings);
            if (!HasWebSocketCredential(settings))
            {
                throw new InvalidOperationException("Run 'xeno login -u <username>' or configure an access key before connecting.");
            }

            ClientConnectionState existing = ClientConnectionState.Load();
            if (existing.IsConnected
                && existing.IsRegistered
                && existing.IsRecordedProcessRunning()
                && existing.HasFreshActivity(DateTime.UtcNow, settings.Server.LivenessTimeoutMs))
            {
                if (_UseJson)
                {
                    WriteJson(new
                    {
                        connected = true,
                        alreadyRunning = true,
                        websocketUrl = settings.Server.Url,
                        restUrl = BuildRestBaseUrl(settings),
                        processId = existing.ProcessId ?? 0,
                        logFile = existing.LogFilePath ?? ClientConnectionState.LogFile,
                        localDashboard = BuildLocalDashboardJson(existing)
                    });
                }
                else
                {
                    WriteInfo("Xeno client is already connected.");
                    PrintServerAccess(settings);
                    PrintStartField("Process ID", existing.ProcessId == null ? "(none)" : existing.ProcessId.Value.ToString(CultureInfo.InvariantCulture));
                    PrintStartField("Log file", DisplayValue(existing.LogFilePath, ClientConnectionState.LogFile));
                    PrintLocalDashboardAccess(existing);
                }

                return 0;
            }

            WriteConnectionAttempt(settings);
            ClientConnectionState.ClearStopRequest();
            ProcessStartInfo startInfo = CreateBackgroundStartInfo();
            using (Process process = new Process { StartInfo = startInfo })
            {
                if (!process.Start()) throw new InvalidOperationException("Unable to start Xeno client background process.");
                bool registered = await WaitForRegisteredStateAsync(process.Id, timeoutMs, token).ConfigureAwait(false);
                ClientConnectionState state = await WaitForLocalDashboardStateAsync(process.Id, settings, registered, token).ConfigureAwait(false);
                if (_UseJson)
                {
                    WriteJson(new
                    {
                        connected = registered,
                        websocketUrl = settings.Server.Url,
                        restUrl = BuildRestBaseUrl(settings),
                        processId = process.Id,
                        logFile = state.LogFilePath ?? ClientConnectionState.LogFile,
                        errorLogFile = state.ErrorLogFilePath ?? ClientConnectionState.ErrorLogFile,
                        reason = state.LastDisconnectReason ?? String.Empty,
                        localDashboard = BuildLocalDashboardJson(state)
                    });
                }
                else if (registered)
                {
                    WriteSuccess("Xeno client connected in the background.");
                    PrintServerAccess(settings);
                    PrintStartField("Process ID", process.Id.ToString(CultureInfo.InvariantCulture));
                    PrintStartField("Log file", DisplayValue(state.LogFilePath, ClientConnectionState.LogFile));
                    PrintLocalDashboardAccess(state);
                }
                else
                {
                    WriteError("Xeno client did not register within " + timeoutMs + "ms.");
                    PrintServerAccess(settings);
                    PrintStartField("Process ID", process.Id.ToString(CultureInfo.InvariantCulture));
                    PrintStartField("Log file", DisplayValue(state.LogFilePath, ClientConnectionState.LogFile));
                    PrintLocalDashboardAccess(state);
                    Console.WriteLine("  Last reason: " + DisplayValue(state.LastDisconnectReason, "(none)"));
                }

                return registered ? 0 : 1;
            }
        }

        private static async Task RunForegroundConnectAsync(ClientSettings settings, bool worker, CancellationToken token)
        {
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
                    await ConnectAsync(settings, runtimeTokenSource.Token).ConfigureAwait(false);
                }
                finally
                {
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
            EnsureClientId(settings);
            if (!HasWebSocketCredential(settings))
            {
                throw new InvalidOperationException("Run 'xeno login -u <username>' or configure an access key before connecting.");
            }

            WriteConnectionAttempt(settings);
            int reconnectDelayMs = settings.Server.ReconnectInitialDelayMs;
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using (ClientWebSocket socket = new ClientWebSocket())
                    {
                        ApplyServerAuthentication(socket, settings);
                        await socket.ConnectAsync(new Uri(settings.Server.Url), token).ConfigureAwait(false);
                        ClientConnectionState.MarkWebSocketConnected(settings);
                        await SendMessageAsync(socket, new { type = "client.register", clientId = settings.ClientId, tenantId = settings.TenantId, name = settings.Name, inventory = settings.BuildInventoryMessage() }, token).ConfigureAwait(false);
                        string? disconnectReason = await ReceiveLoopAsync(socket, settings, token).ConfigureAwait(false);
                        ClientConnectionState.MarkDisconnected(settings, disconnectReason ?? "WebSocket closed.");
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    ClientConnectionState.MarkDisconnected(settings, "Client stopped.");
                    return;
                }
                catch (Exception ex)
                {
                    ClientConnectionState.MarkDisconnected(settings, ex.Message);
                    WriteWarning("Disconnected from Xeno server: " + ex.Message);
                }

                if (ClientConnectionState.IsStopRequested())
                {
                    ClientConnectionState.MarkDisconnected(settings, "Client stopped.");
                    return;
                }

                await DelayBeforeReconnectAsync(reconnectDelayMs, settings, token).ConfigureAwait(false);
                reconnectDelayMs = NextReconnectDelayMs(reconnectDelayMs, settings);
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

                    await SendMessageAsync(socket, new { type = "client.heartbeat", clientId = settings.ClientId, tenantId = settings.TenantId, sentUtc = DateTime.UtcNow }, token).ConfigureAwait(false);
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
                        WriteInfo("Sent client inventory update to Xeno server.");
                        ClientConnectionState.MarkInventoryUpdateSent();
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
            if (String.Equals(type, "server.telemetry.request", StringComparison.OrdinalIgnoreCase))
            {
                ClientSettings runtimeSettings = LoadRuntimeSettingsForConnectedSession(settings);
                await HandleServerTelemetryRequestAsync(socket, runtimeSettings, message, token).ConfigureAwait(false);
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
                ? (settings.Runners.Count > 0 ? settings.Runners[0] : null)
                : settings.Runners.Find(item => String.Equals(item.Name, sourceName, StringComparison.OrdinalIgnoreCase));

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
                    Error = "No configured provider endpoint is available."
                };
                await LocalDashboardRuntime.RecordCompletedAsync(requestId, response, false, token).ConfigureAwait(false);
                return response;
            }

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

        private static bool HasWebSocketCredential(ClientSettings settings)
        {
            return !String.IsNullOrWhiteSpace(settings.Server.AccessKey);
        }

        private static void SaveInventorySettings(ClientSettings settings)
        {
            settings.Save();
            ClientConnectionState.MarkInventoryUpdateRequested();
        }

        private static ClientSettings LoadRuntimeSettingsForConnectedSession(ClientSettings connectedSettings)
        {
            ClientSettings latestSettings = ClientSettings.Load();
            if (!String.IsNullOrWhiteSpace(connectedSettings.ClientId)) latestSettings.ClientId = connectedSettings.ClientId;
            if (!String.IsNullOrWhiteSpace(connectedSettings.TenantId)) latestSettings.TenantId = connectedSettings.TenantId;
            return latestSettings;
        }

        private static string EnsureClientId(ClientSettings settings)
        {
            if (String.IsNullOrWhiteSpace(settings.ClientId))
            {
                settings.ClientId = IdGenerator.NewId("xcl_");
                settings.Save();
            }

            return settings.ClientId;
        }

        private static Uri BuildRestUri(ClientSettings settings, string path)
        {
            string source = String.IsNullOrWhiteSpace(settings.Server.RestUrl) ? settings.Server.Url : settings.Server.RestUrl;
            UriBuilder builder = new UriBuilder(source);
            if (String.Equals(builder.Scheme, "ws", StringComparison.OrdinalIgnoreCase)) builder.Scheme = "http";
            if (String.Equals(builder.Scheme, "wss", StringComparison.OrdinalIgnoreCase)) builder.Scheme = "https";
            builder.Path = path.TrimStart('/');
            builder.Query = String.Empty;
            return builder.Uri;
        }

        private static Uri BuildPingUri(Uri endpoint, int sequence, DateTime clientSentUtc)
        {
            UriBuilder builder = new UriBuilder(endpoint);
            string sequenceValue = sequence.ToString(CultureInfo.InvariantCulture);
            string sentValue = Uri.EscapeDataString(clientSentUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            builder.Query = "sequence=" + sequenceValue + "&clientSentUtc=" + sentValue;
            return builder.Uri;
        }

        private static string BuildRestBaseUrl(ClientSettings settings)
        {
            string source = String.IsNullOrWhiteSpace(settings.Server.RestUrl) ? settings.Server.Url : settings.Server.RestUrl;
            UriBuilder builder = new UriBuilder(source);
            if (String.Equals(builder.Scheme, "ws", StringComparison.OrdinalIgnoreCase)) builder.Scheme = "http";
            if (String.Equals(builder.Scheme, "wss", StringComparison.OrdinalIgnoreCase)) builder.Scheme = "https";
            builder.Path = String.Empty;
            builder.Query = String.Empty;
            return builder.Uri.ToString().TrimEnd('/');
        }

        private static string TryReadJsonProperty(string json, string propertyName)
        {
            if (String.IsNullOrWhiteSpace(json)) return String.Empty;
            if (!JsonUtil.TryDeserialize<ClientPingResponse>(json, out ClientPingResponse? response) || response == null) return String.Empty;
            if (String.Equals(propertyName, "status", StringComparison.OrdinalIgnoreCase))
            {
                return response.Status;
            }

            if (String.Equals(propertyName, "serverUtc", StringComparison.OrdinalIgnoreCase))
            {
                return response.ServerUtc;
            }

            return String.Empty;
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

        private static async Task<bool> TestRunnerAsync(ClientRunnerSettings runner, CancellationToken token)
        {
            string relativePath = String.Equals(runner.ApiType, "Ollama", StringComparison.OrdinalIgnoreCase) ? "/api/tags" : "/v1/models";
            string endpoint = runner.Endpoint.TrimEnd('/') + relativePath;
            using (HttpClient httpClient = new HttpClient())
            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, endpoint))
            {
                httpClient.Timeout = TimeSpan.FromMilliseconds(Math.Max(1000, runner.TimeoutMs));
                if (!String.IsNullOrWhiteSpace(runner.Credential))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", runner.Credential);
                }

                try
                {
                    HttpResponseMessage result = await httpClient.SendAsync(request, token).ConfigureAwait(false);
                    using (result)
                    {
                        return result.IsSuccessStatusCode;
                    }
                }
                catch (HttpRequestException)
                {
                    return false;
                }
                catch (TaskCanceledException)
                {
                    return false;
                }
            }
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

        private static ProcessStartInfo CreateBackgroundStartInfo()
        {
            string? processPath = Environment.ProcessPath;
            if (String.IsNullOrWhiteSpace(processPath))
            {
                using (Process process = Process.GetCurrentProcess())
                {
                    processPath = process.MainModule?.FileName;
                }
            }

            if (String.IsNullOrWhiteSpace(processPath)) throw new InvalidOperationException("Unable to determine the current executable path.");
            string processName = Path.GetFileNameWithoutExtension(processPath);
            bool isDotnetHost = String.Equals(processName, "dotnet", StringComparison.OrdinalIgnoreCase);
            bool isWindows = OperatingSystem.IsWindows();
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = processPath,
                CreateNoWindow = true,
                RedirectStandardError = !isWindows,
                RedirectStandardOutput = !isWindows,
                UseShellExecute = isWindows,
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = Environment.CurrentDirectory
            };

            if (isDotnetHost) startInfo.ArgumentList.Add(typeof(Program).Assembly.Location);
            startInfo.ArgumentList.Add("--no-color");
            startInfo.ArgumentList.Add("connect");
            startInfo.ArgumentList.Add("--foreground");
            startInfo.ArgumentList.Add("--worker");

            if (!isWindows)
            {
                string? clientHome = Environment.GetEnvironmentVariable("XENO_CLIENT_HOME");
                if (!String.IsNullOrWhiteSpace(clientHome)) startInfo.Environment["XENO_CLIENT_HOME"] = clientHome;
                startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
                startInfo.Environment["XENO_CLIENT_BACKGROUND"] = "1";
            }

            return startInfo;
        }

        private static async Task<bool> WaitForRegisteredStateAsync(int processId, int timeoutMs, CancellationToken token)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline && !token.IsCancellationRequested)
            {
                ClientConnectionState state = ClientConnectionState.Load();
                if (state.ProcessId == processId && state.IsConnected && state.IsRegistered) return true;
                if (!IsProcessRunning(processId)) return false;
                await Task.Delay(250, token).ConfigureAwait(false);
            }

            return false;
        }

        private static async Task<ClientConnectionState> WaitForLocalDashboardStateAsync(int processId, ClientSettings settings, bool registered, CancellationToken token)
        {
            ClientConnectionState state = ClientConnectionState.Load();
            if (!settings.LocalDashboard.Enabled || !registered) return state;

            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline && !token.IsCancellationRequested)
            {
                state = ClientConnectionState.Load();
                if (state.ProcessId == processId
                    && state.LocalDashboardEnabled
                    && (state.LocalDashboardListening || !String.IsNullOrWhiteSpace(state.LocalDashboardLastError)))
                {
                    return state;
                }

                if (!IsProcessRunning(processId)) return state;
                await Task.Delay(100, token).ConfigureAwait(false);
            }

            return ClientConnectionState.Load();
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

        private static async Task<bool> WaitForProcessExitAsync(int processId, int timeoutMs)
        {
            try
            {
                using (Process process = Process.GetProcessById(processId))
                {
                    using (CancellationTokenSource timeout = new CancellationTokenSource(timeoutMs))
                    {
                        try
                        {
                            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
                            return true;
                        }
                        catch (OperationCanceledException)
                        {
                            return process.HasExited;
                        }
                    }
                }
            }
            catch (ArgumentException)
            {
                return true;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
        }

        private static bool KillProcess(int processId)
        {
            try
            {
                using (Process process = Process.GetProcessById(processId))
                {
                    process.Kill(true);
                    process.WaitForExit(DefaultStopTimeoutMs);
                    return process.HasExited;
                }
            }
            catch (ArgumentException)
            {
                return true;
            }
            catch (InvalidOperationException)
            {
                return true;
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

        private static async Task PrintLogTailAsync(string logFile, int tailLines, CancellationToken token)
        {
            if (tailLines == 0) return;
            List<string> lines = new List<string>();
            using (FileStream stream = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (StreamReader reader = new StreamReader(stream))
            {
                while (!token.IsCancellationRequested)
                {
                    string? line = await reader.ReadLineAsync(token).ConfigureAwait(false);
                    if (line == null) break;
                    lines.Add(line);
                    if (lines.Count > tailLines) lines.RemoveAt(0);
                }
            }

            foreach (string line in lines)
            {
                Console.WriteLine(line);
            }
        }

        private static async Task FollowLogAsync(string logFile, CancellationToken token)
        {
            using (FileStream stream = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (StreamReader reader = new StreamReader(stream))
            {
                stream.Seek(0, SeekOrigin.End);
                while (!token.IsCancellationRequested)
                {
                    string? line = await reader.ReadLineAsync(token).ConfigureAwait(false);
                    if (line != null)
                    {
                        Console.WriteLine(line);
                        continue;
                    }

                    await Task.Delay(250, token).ConfigureAwait(false);
                }
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
            Console.WriteLine("  Access key: " + DisplayValue(settings.Server.AccessKey, "(not set)"));
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
                    authenticated = HasWebSocketCredential(settings),
                    auth = DescribeAuth(settings),
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
            Console.WriteLine("  Auth:       " + DescribeAuth(settings));
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
                    Console.WriteLine("  Dashboard bind:  0.0.0.0 (may be reachable from other machines depending on firewall and network policy)");
                }

                return;
            }

            PrintStartField("Local dashboard", "enabled, not listening");
            if (!String.IsNullOrWhiteSpace(state.LocalDashboardLastError)) Console.WriteLine("  Dashboard error: " + state.LocalDashboardLastError);
        }

        private static void PrintServerAccess(ClientSettings settings)
        {
            PrintStartField("Server WebSocket", settings.Server.Url);
            PrintStartField("Server REST", BuildRestBaseUrl(settings));
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
                    credentialConfigured = !String.IsNullOrWhiteSpace(runner.Credential),
                    models = runner.Models.ConvertAll(model => new
                    {
                        name = model.Name,
                        supportsCompletions = model.SupportsCompletions,
                        supportsEmbeddings = model.SupportsEmbeddings
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
                Console.WriteLine("    Models:     " + runner.Models.Count);
                if (includeModelDetails && runner.Models.Count > 0)
                {
                    foreach (ClientModelSettings model in runner.Models)
                    {
                        Console.WriteLine("      - " + model.Name);
                        Console.WriteLine("        Completions: " + FormatBool(model.SupportsCompletions));
                        Console.WriteLine("        Embeddings:  " + FormatBool(model.SupportsEmbeddings));
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

        private static ClientRunnerSettings? FindDuplicateProvider(ClientSettings settings, ClientRunnerSettings candidate)
        {
            string candidateEndpoint = NormalizeEndpoint(candidate.Endpoint);
            foreach (ClientRunnerSettings existing in settings.Runners)
            {
                if (String.Equals(existing.Name, candidate.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return existing;
                }

                if (!String.IsNullOrWhiteSpace(candidateEndpoint) &&
                    String.Equals(NormalizeEndpoint(existing.Endpoint), candidateEndpoint, StringComparison.OrdinalIgnoreCase))
                {
                    return existing;
                }
            }

            return null;
        }

        private static ClientRunnerSettings? FindModelAddProvider(ClientSettings settings, string providerName)
        {
            return settings.Runners.Find(item => String.Equals(item.Name, providerName, StringComparison.OrdinalIgnoreCase));
        }

        private static string NormalizeEndpoint(string? endpoint)
        {
            return String.IsNullOrWhiteSpace(endpoint) ? String.Empty : endpoint.Trim().TrimEnd('/');
        }

        private static string DescribeAuth(ClientSettings settings)
        {
            if (!String.IsNullOrWhiteSpace(settings.Server.AccessKey))
            {
                return "access key " + settings.Server.AccessKey;
            }

            if (!String.IsNullOrWhiteSpace(settings.Server.Token)) return "REST token only";
            return "not configured";
        }

        private static string DisplayValue(string? value, string emptyText)
        {
            return String.IsNullOrWhiteSpace(value) ? emptyText : value;
        }

        private static string MaskSecret(string? value)
        {
            if (String.IsNullOrWhiteSpace(value)) return "(not set)";
            if (value.Length <= 4) return "configured";
            return "configured (..." + value.Substring(value.Length - 4) + ")";
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

        private static string FormatHttpError(HttpResponseMessage response, string responseBody)
        {
            string? message = TryGetErrorMessage(responseBody);
            if (String.IsNullOrWhiteSpace(message)) message = responseBody.Trim();
            if (String.IsNullOrWhiteSpace(message)) message = response.ReasonPhrase ?? "Request failed";
            if (message.Length > 300) message = message.Substring(0, 300) + "...";
            return ((int)response.StatusCode) + " " + response.ReasonPhrase + " - " + message;
        }

        private static string? TryGetErrorMessage(string responseBody)
        {
            if (String.IsNullOrWhiteSpace(responseBody)) return null;
            try
            {
                ClientErrorResponse? error = JsonUtil.Deserialize<ClientErrorResponse>(responseBody);
                return error?.Message
                    ?? error?.Error
                    ?? error?.Title
                    ?? error?.Detail;
            }
            catch (InvalidDataException)
            {
                return null;
            }
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
            Console.WriteLine("      Show the client configuration, including the local dashboard URL when enabled.");
            Console.WriteLine("  xeno config colors on");
            Console.WriteLine("      Enable colored command output.");
            Console.WriteLine("  xeno config colors off");
            Console.WriteLine("      Disable colored command output.");
        }

        private static void PrintServerHelp()
        {
            WriteInfoHeading("Server commands");
            Console.WriteLine("  xeno server set-url <websocket-url>");
            Console.WriteLine("      Set the Xeno server WebSocket endpoint.");
            Console.WriteLine("  xeno server set-rest-url <rest-url>");
            Console.WriteLine("      Set the Xeno server REST API endpoint.");
        }

        private static void PrintResetHelp()
        {
            WriteInfoHeading("Reset command");
            Console.WriteLine("  xeno reset");
            Console.WriteLine("      Prompt for confirmation, then restore ~/.xeno/client.json to factory defaults and remove runtime state.");
            Console.WriteLine("  xeno reset --force");
            Console.WriteLine("      Restore factory defaults without prompting.");
        }

        private static void PrintLoginHelp()
        {
            WriteInfoHeading("Login command");
            Console.WriteLine("  xeno login -u <username>");
            Console.WriteLine("      Prompt for a password and store only the returned client credential.");
            Console.WriteLine("  xeno login -u <username> --tenant <tenant-id> --server <rest-url>");
            Console.WriteLine("      Login against a specific tenant and REST server.");
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
            Console.WriteLine("  xeno provider add --name <name> --type <api-type> --endpoint <url> [--credential <value>]");
            Console.WriteLine("      Add an Ollama, OpenAI, Gemini, or compatible endpoint.");
            Console.WriteLine("  xeno provider update --name <name> [--new-name <name>] [--type <api-type>] [--endpoint <url>] [--timeout <ms>] [--credential <value>|--clear-credential]");
            Console.WriteLine("      Update a configured provider endpoint and request inventory re-advertisement when connected.");
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
            Console.WriteLine("  xeno model delete --name <model-name> [--provider <provider-name>]");
            Console.WriteLine("      Aliases: remove, remote, rm.");
            Console.WriteLine("      Delete a model from one provider, or from all providers when omitted.");
        }

        private static void PrintConnectHelp()
        {
            WriteInfoHeading("Connect commands");
            Console.WriteLine("  xeno connect");
            Console.WriteLine("  xeno start");
            Console.WriteLine("      Start the client as a background process and return after registration succeeds. Prints the local dashboard URL when enabled.");
            Console.WriteLine("  xeno connect --foreground");
            Console.WriteLine("      Run the client WebSocket loop in the current terminal. Prints the local dashboard URL when enabled.");
            Console.WriteLine("  xeno connect --timeout <milliseconds>");
            Console.WriteLine("      Wait this long for background registration before returning an error.");
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
        }

        private static void PrintDashboardHelp()
        {
            WriteInfoHeading("Dashboard commands");
            Console.WriteLine("  xeno dashboard");
            Console.WriteLine("      Show the local dashboard URL when LocalDashboard.Enabled is true and the worker is listening.");
            Console.WriteLine("  xeno dashboard open");
            Console.WriteLine("      Open the local dashboard in the default browser.");
        }

        private static void PrintReloadHelp()
        {
            WriteInfoHeading("Reload command");
            Console.WriteLine("  xeno reload");
            Console.WriteLine("      Reload ~/.xeno/client.json from disk and request provider/model inventory re-advertisement when connected.");
        }

        private static void PrintStopHelp()
        {
            WriteInfoHeading("Stop commands");
            Console.WriteLine("  xeno stop");
            Console.WriteLine("  xeno disconnect");
            Console.WriteLine("      Request the background Xeno client process to stop.");
            Console.WriteLine("  xeno stop --force");
            Console.WriteLine("      Request stop, then terminate the recorded process if it does not exit.");
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
            WriteColored("Error: " + message, ConsoleColor.Red);
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
            Console.WriteLine("  xeno connect --foreground");
            Console.WriteLine("  xeno logs -f");
            Console.WriteLine("  xeno dashboard");
            Console.WriteLine("  xeno dashboard open");
            Console.WriteLine("  xeno stop");
            Console.WriteLine("  xeno disconnect");
            WritePlainHeading("Configuration:");
            Console.WriteLine("  xeno config show");
            Console.WriteLine("  xeno config colors on");
            Console.WriteLine("  xeno config colors off");
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
            Console.WriteLine("  xeno provider add --name <name> --type <api-type> --endpoint <url> [--credential <value>]");
            Console.WriteLine("  xeno provider update --name <name> [--new-name <name>] [--type <api-type>] [--endpoint <url>] [--timeout <ms>] [--credential <value>|--clear-credential]");
            WritePlainHeading("Models:");
            Console.WriteLine("  xeno model list");
            Console.WriteLine("  xeno model ls");
            Console.WriteLine("  xeno model add --provider <provider-name> --name <model-name> [--type completion|embedding|both]");
            Console.WriteLine("  xeno model update --name <model-name> [--provider <provider-name>] [--new-name <model-name>] [--new-provider <provider-name>] [--type completion|embedding|both]");
            Console.WriteLine("  xeno model delete --name <model-name> [--provider <provider-name>]  (aliases: remove, remote, rm)");
            WritePlainHeading("Runtime:");
            Console.WriteLine("  xeno reload");
            Console.WriteLine("  xeno connect");
            Console.WriteLine("  xeno start");
            Console.WriteLine("  xeno connect --foreground");
            Console.WriteLine("  xeno logs [--tail <lines>] [-f]");
            Console.WriteLine("  xeno ping [-n <count>] [-w <milliseconds>] [--server <rest-url>]");
            Console.WriteLine("  xeno dashboard [open]");
            Console.WriteLine("  xeno stop [--force]");
            Console.WriteLine("  xeno disconnect");
            Console.WriteLine();
            Console.WriteLine("When LocalDashboard.Enabled is true, 'xeno connect', 'xeno status', and 'xeno dashboard' show the local dashboard URL.");
            WritePlainHeading("Global options:");
            Console.WriteLine("  --no-color    Disable colored output for this command.");
            Console.WriteLine("  --color       Enable colored output for this command.");
            Console.WriteLine("  --json        Emit machine-readable JSON for supported commands.");
            WritePlainHeading("Reset:");
            Console.WriteLine("  xeno reset");
            Console.WriteLine("  xeno reset --force");
            Console.WriteLine();
            Console.WriteLine("Use 'xeno login' when you have a username/password. Use 'xeno set-auth' when you already have an access key.");
        }
    }
}

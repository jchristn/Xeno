namespace Xeno.Client
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Net.Http;
    using System.Net.WebSockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Enums;
    using Xeno.Core.Helpers;
    using Xeno.Core.Models;
    using Xeno.Core.Serialization;

    /// <summary>
    /// Xeno client CLI entry point.
    /// </summary>
    public static class Program
    {
        private const string ClientVersion = "0.1.0";
        private const int DefaultHeartbeatIntervalMs = 15000;
        private static bool _UseColors = true;
        private static bool _UseJson = false;
        private static readonly SemaphoreSlim _SendLock = new SemaphoreSlim(1, 1);

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
                        case "status":
                            PrintStatus(settings);
                            return 0;
                        case "connect":
                            await ConnectAsync(settings, CancellationToken.None).ConfigureAwait(false);
                            return 0;
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
                WriteWarning("Saved a REST token. WebSocket connections require xeno login or xeno set-auth --access-key/--secret-key.");
                return 0;
            }

            if (args.Length >= 4 && args[1] == "set-credential")
            {
                return HandleSetAuth(new[] { "set-auth", "--access-key", args[2], "--secret-key", args[3] }, settings);
            }

            PrintAuthHelp();
            return 1;
        }

        private static int HandleSetAuth(string[] args, ClientSettings settings)
        {
            string? accessKey = null;
            string? secretKey = null;
            bool clear = false;

            if (args.Length == 3 && !args[1].StartsWith("-", StringComparison.Ordinal) && !args[2].StartsWith("-", StringComparison.Ordinal))
            {
                accessKey = args[1];
                secretKey = args[2];
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

                if (args[i] == "--secret-key" && i + 1 < args.Length)
                {
                    secretKey = args[++i];
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

            if (!String.IsNullOrWhiteSpace(accessKey) && !String.IsNullOrWhiteSpace(secretKey))
            {
                settings.Server.AccessKey = accessKey;
                settings.Server.SecretKey = secretKey;
                settings.Server.Token = null;
                settings.Save();
                WriteSuccess("Saved access key and secret key for server authentication.");
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
                    string? secretKey = credential?.SecretKey;
                    string? returnedTenantId = credential?.TenantId;
                    if (String.IsNullOrWhiteSpace(accessKey) || String.IsNullOrWhiteSpace(secretKey))
                    {
                        WriteError("Login response did not include a client credential.");
                        return 1;
                    }

                    settings.TenantId = String.IsNullOrWhiteSpace(returnedTenantId) ? tenantId : returnedTenantId;
                    settings.Server.AccessKey = accessKey;
                    settings.Server.SecretKey = secretKey;
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

            if (args.Length >= 2 && args[1] == "list")
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

            if (args.Length >= 2 && (args[1] == "delete" || args[1] == "remove"))
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

                settings.Save();
                if (removed == 1) WriteSuccess("Deleted provider endpoint '" + runnerName + "'.");
                else WriteSuccess("Deleted provider endpoint '" + runnerName + "' (" + removed + " entries removed).");
                return 0;
            }

            if (args.Length >= 2 && args[1] == "add")
            {
                ClientRunnerSettings runner = new ClientRunnerSettings();
                runner.Name = String.Empty;
                runner.Provider = String.Empty;
                runner.Endpoint = String.Empty;
                for (int i = 2; i < args.Length - 1; i += 2)
                {
                    if (args[i] == "--name") runner.Name = args[i + 1];
                    if (args[i] == "--provider") runner.Provider = args[i + 1];
                    if (args[i] == "--endpoint") runner.Endpoint = args[i + 1];
                    if (args[i] == "--credential") runner.Credential = args[i + 1];
                }

                if (String.IsNullOrWhiteSpace(runner.Name) || String.IsNullOrWhiteSpace(runner.Provider) || String.IsNullOrWhiteSpace(runner.Endpoint))
                {
                    List<string> missing = new List<string>();
                    if (String.IsNullOrWhiteSpace(runner.Name)) missing.Add("--name");
                    if (String.IsNullOrWhiteSpace(runner.Provider)) missing.Add("--provider");
                    if (String.IsNullOrWhiteSpace(runner.Endpoint)) missing.Add("--endpoint");
                    WriteError("Missing required option(s): " + String.Join(", ", missing) + ".");
                    PrintProviderHelp();
                    return 1;
                }

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
                settings.Save();
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

            if (args.Length >= 2 && args[1] == "list")
            {
                PrintModels(settings);
                return 0;
            }

            if (args.Length >= 2 && args[1] == "add")
            {
                string runnerName = String.Empty;
                string modelName = String.Empty;
                for (int i = 2; i < args.Length - 1; i += 2)
                {
                    if (args[i] == "--runner" || args[i] == "--provider") runnerName = args[i + 1];
                    if (args[i] == "--name") modelName = args[i + 1];
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

                ClientRunnerSettings? runner = settings.Runners.Find(item => String.Equals(item.Name, runnerName, StringComparison.OrdinalIgnoreCase));
                if (runner == null)
                {
                    WriteError("Provider endpoint not found: " + runnerName);
                    return 1;
                }

                runner.Models.Add(new ClientModelSettings { Name = modelName });
                settings.Save();
                WriteSuccess("Added model '" + modelName + "' to provider endpoint '" + runner.Name + "'.");
                return 0;
            }

            if (args.Length >= 2 && (args[1] == "delete" || args[1] == "remove"))
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

                settings.Save();
                if (!String.IsNullOrWhiteSpace(runnerName)) WriteSuccess("Deleted model '" + modelName + "' from provider endpoint '" + runnerName + "'.");
                else if (removed == 1) WriteSuccess("Deleted model '" + modelName + "'.");
                else WriteSuccess("Deleted model '" + modelName + "' (" + removed + " entries removed).");
                return 0;
            }

            WriteError("Unknown model command.");
            PrintModelHelp();
            return 1;
        }

        private static async Task ConnectAsync(ClientSettings settings, CancellationToken token)
        {
            EnsureClientId(settings);
            if (!HasWebSocketCredential(settings))
            {
                throw new InvalidOperationException("Run 'xeno login -u <username>' or configure an access key and secret key before connecting.");
            }

            while (!token.IsCancellationRequested)
            {
                try
                {
                    using (ClientWebSocket socket = new ClientWebSocket())
                    {
                        ApplyServerAuthentication(socket, settings);
                        await socket.ConnectAsync(new Uri(settings.Server.Url), token).ConfigureAwait(false);
                        ClientConnectionState.MarkConnected(settings);
                        await SendMessageAsync(socket, new { type = "client.register", clientId = settings.ClientId, tenantId = settings.TenantId, name = settings.Name, inventory = settings.BuildInventory() }, token).ConfigureAwait(false);
                        await ReceiveLoopAsync(socket, settings, token).ConfigureAwait(false);
                        ClientConnectionState.MarkDisconnected(settings, "WebSocket closed.");
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

                await Task.Delay(TimeSpan.FromSeconds(5), token).ConfigureAwait(false);
            }
        }

        private static async Task ReceiveLoopAsync(ClientWebSocket socket, ClientSettings settings, CancellationToken token)
        {
            byte[] buffer = new byte[65536];
            using (CancellationTokenSource heartbeatTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                Task heartbeatTask = SendHeartbeatLoopAsync(socket, settings, heartbeatTokenSource.Token);
                try
                {
                    while (!token.IsCancellationRequested && socket.State == WebSocketState.Open)
                    {
                        WebSocketReceiveResult result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token).ConfigureAwait(false);
                        if (result.MessageType == WebSocketMessageType.Close) return;

                        string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        ClientConnectionState.MarkServerMessage(settings);
                        PrintServerMessage(message);
                        await HandleServerMessageAsync(socket, settings, message, token).ConfigureAwait(false);
                    }
                }
                finally
                {
                    heartbeatTokenSource.Cancel();
                    await heartbeatTask.ConfigureAwait(false);
                }
            }
        }

        private static async Task SendHeartbeatLoopAsync(ClientWebSocket socket, ClientSettings settings, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(DefaultHeartbeatIntervalMs), token).ConfigureAwait(false);
                    if (token.IsCancellationRequested || socket.State != WebSocketState.Open) return;

                    await SendMessageAsync(socket, new { type = "client.heartbeat", clientId = settings.ClientId, tenantId = settings.TenantId, sentUtc = DateTime.UtcNow }, token).ConfigureAwait(false);
                    ClientConnectionState.MarkHeartbeat(settings);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    return;
                }
                catch (InvalidOperationException)
                {
                    return;
                }
                catch (WebSocketException)
                {
                    return;
                }
            }
        }

        private static async Task HandleServerMessageAsync(ClientWebSocket socket, ClientSettings settings, string message, CancellationToken token)
        {
            if (message.IndexOf("\"server.request\"", StringComparison.OrdinalIgnoreCase) < 0) return;

            object? response = await ExecuteServerRequestAsync(socket, settings, message, token).ConfigureAwait(false);
            if (response != null) await SendMessageAsync(socket, response, token).ConfigureAwait(false);
        }

        private static async Task<object?> ExecuteServerRequestAsync(ClientWebSocket socket, ClientSettings settings, string message, CancellationToken token)
        {
            ServerRequestMessage? serverRequest = JsonUtil.Deserialize<ServerRequestMessage>(message);
            if (serverRequest == null) return null;

            string requestId = String.IsNullOrWhiteSpace(serverRequest.RequestId) ? Guid.NewGuid().ToString("N") : serverRequest.RequestId;
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

            if (runner == null)
            {
                stopwatch.Stop();
                WriteRequestCompleted(requestId, requestorLabel, method, relativePath, proxy, stopwatch.Elapsed.TotalMilliseconds);
                return new ClientResponseMessage
                {
                    Type = "client.response",
                    RequestId = requestId,
                    ClientId = settings.ClientId,
                    TenantId = settings.TenantId,
                    Success = false,
                    Error = "No configured provider endpoint is available."
                };
            }

            Dictionary<string, string> forwardedHeaders = serverRequest.Headers ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string body = String.IsNullOrWhiteSpace(serverRequest.Body) ? "{}" : serverRequest.Body;
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
                return new ClientResponseMessage
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
            await SendMessageAsync(socket, new ClientResponseMessage
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
            }, token).ConfigureAwait(false);

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
                }

                stopwatch.Stop();
                string body = Encoding.UTF8.GetString(captured.ToArray());
                int streamEvents = CountStreamEvents(result.Content.Headers.ContentType?.ToString(), body, sequence);
                await SendMessageAsync(socket, new ClientResponseMessage
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
                }, token).ConfigureAwait(false);
                WriteRequestCompleted(requestId, requestorIp, method, relativePath, proxy, stopwatch.Elapsed.TotalMilliseconds);
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
            return envelope?.Usage ?? new OpenAiUsage();
        }

        private static void ApplyServerAuthentication(ClientWebSocket socket, ClientSettings settings)
        {
            if (!String.IsNullOrWhiteSpace(settings.Server.AccessKey))
            {
                socket.Options.SetRequestHeader("x-access-key", settings.Server.AccessKey);
            }

            if (!String.IsNullOrWhiteSpace(settings.Server.SecretKey))
            {
                socket.Options.SetRequestHeader("x-secret-key", settings.Server.SecretKey);
            }

            socket.Options.SetRequestHeader("x-tenant-guid", settings.TenantId);
        }

        private static bool HasWebSocketCredential(ClientSettings settings)
        {
            bool hasDirectCredential = !String.IsNullOrWhiteSpace(settings.Server.AccessKey) &&
                                       !String.IsNullOrWhiteSpace(settings.Server.SecretKey);
            return hasDirectCredential;
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
            string relativePath = String.Equals(runner.Provider, "Ollama", StringComparison.OrdinalIgnoreCase) ? "/api/tags" : "/v1/models";
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
            Console.WriteLine("  Secret key: " + MaskSecret(settings.Server.SecretKey));
            Console.WriteLine("  REST token: " + MaskSecret(settings.Server.Token));
            PrintProviders(settings);
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
            if (_UseJson)
            {
                Console.WriteLine(message);
                return;
            }

            try
            {
                WebSocketMessageEnvelope? envelope = JsonUtil.Deserialize<WebSocketMessageEnvelope>(message);
                string? type = envelope?.Type;
                if (String.Equals(type, "server.heartbeat", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                if (String.Equals(type, "server.connected", StringComparison.OrdinalIgnoreCase))
                {
                    WriteSuccess("Connected to Xeno server.");
                    return;
                }

                if (String.Equals(type, "server.registered", StringComparison.OrdinalIgnoreCase))
                {
                    WriteSuccess("Client registered with Xeno server.");
                    return;
                }

                if (String.Equals(type, "server.request", StringComparison.OrdinalIgnoreCase))
                {
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

                if (String.Equals(type, "server.error", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(type, "server.unauthorized", StringComparison.OrdinalIgnoreCase))
                {
                    ClientErrorResponse? error = JsonUtil.Deserialize<ClientErrorResponse>(message);
                    WriteError(error?.Message ?? "The Xeno server rejected the client message.");
                    return;
                }

                if (!String.IsNullOrWhiteSpace(type))
                {
                    WriteInfo("Server message: " + type + ".");
                    return;
                }
            }
            catch (InvalidDataException)
            {
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
                        connectedUtc = FormatJsonUtc(connection.ConnectedUtc),
                        disconnectedUtc = FormatJsonUtc(connection.DisconnectedUtc),
                        lastHeartbeatUtc = FormatJsonUtc(connection.LastHeartbeatUtc),
                        lastServerMessageUtc = FormatJsonUtc(connection.LastServerMessageUtc),
                        lastUpdatedUtc = FormatJsonUtc(connection.LastUpdatedUtc),
                        processId = connection.ProcessId ?? 0,
                        reason = connection.LastDisconnectReason ?? String.Empty
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
            Console.WriteLine("  Connected UTC:       " + FormatUtc(connection.ConnectedUtc));
            Console.WriteLine("  Disconnected UTC:    " + FormatUtc(connection.DisconnectedUtc));
            Console.WriteLine("  Last heartbeat UTC:  " + FormatUtc(connection.LastHeartbeatUtc));
            Console.WriteLine("  Last server msg UTC: " + FormatUtc(connection.LastServerMessageUtc));
            Console.WriteLine("  Last updated UTC:    " + FormatUtc(connection.LastUpdatedUtc));
            Console.WriteLine("  Process ID:          " + (connection.ProcessId == null ? "(none)" : connection.ProcessId.Value.ToString()));
            Console.WriteLine("  Last reason:         " + DisplayValue(connection.LastDisconnectReason, "(none)"));
        }

        private static void PrintProviders(ClientSettings settings)
        {
            if (_UseJson)
            {
                WriteJson(settings.Runners.ConvertAll(runner => new
                {
                    name = runner.Name,
                    provider = runner.Provider,
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
                Console.WriteLine("    Type:       " + runner.Provider);
                Console.WriteLine("    Endpoint:   " + runner.Endpoint);
                Console.WriteLine("    Timeout:    " + runner.TimeoutMs + " ms");
                Console.WriteLine("    Credential: " + (String.IsNullOrWhiteSpace(runner.Credential) ? "not configured" : "configured"));
                Console.WriteLine("    Models:     " + runner.Models.Count);
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
                            provider = runner.Provider,
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
                    Console.WriteLine("    Provider:    " + runner.Name + " [" + runner.Provider + "]");
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

        private static string NormalizeEndpoint(string? endpoint)
        {
            return String.IsNullOrWhiteSpace(endpoint) ? String.Empty : endpoint.Trim().TrimEnd('/');
        }

        private static string DescribeAuth(ClientSettings settings)
        {
            if (!String.IsNullOrWhiteSpace(settings.Server.AccessKey) && !String.IsNullOrWhiteSpace(settings.Server.SecretKey))
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

        private static void PrintConfigHelp()
        {
            WriteInfoHeading("Configuration commands");
            Console.WriteLine("  xeno config show");
            Console.WriteLine("      Show the client configuration in formatted text.");
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
            Console.WriteLine("  xeno login -u <username>");
            Console.WriteLine("             --tenant <tenant-id>");
            Console.WriteLine("             --server <rest-url>");
            Console.WriteLine("      Login against a specific tenant and REST server.");
        }

        private static void PrintAuthHelp()
        {
            WriteInfoHeading("Authentication commands");
            Console.WriteLine("  xeno login -u <username>");
            Console.WriteLine("             [--tenant <tenant-id>]");
            Console.WriteLine("             [--server <rest-url>]");
            Console.WriteLine("      Prompt for a password and store only the returned access key/secret.");
            Console.WriteLine("  xeno set-auth --access-key <key>");
            Console.WriteLine("                --secret-key <secret>");
            Console.WriteLine("      Store an existing client credential for WebSocket and API use.");
            Console.WriteLine("  xeno set-auth --clear");
            Console.WriteLine("      Remove stored authentication.");
        }

        private static void PrintProviderHelp()
        {
            WriteInfoHeading("Provider commands");
            Console.WriteLine("  xeno provider list");
            Console.WriteLine("      Show configured provider endpoints.");
            Console.WriteLine("  xeno provider test --name <name>");
            Console.WriteLine("      Test connectivity to one configured provider endpoint.");
            Console.WriteLine("  xeno provider delete --name <name>");
            Console.WriteLine("      Delete one configured provider endpoint.");
            Console.WriteLine("  xeno provider add --name <name>");
            Console.WriteLine("                    --provider <provider>");
            Console.WriteLine("                    --endpoint <url>");
            Console.WriteLine("                    [--credential <value>]");
            Console.WriteLine("      Add an Ollama, OpenAI, Gemini, or compatible endpoint.");
        }

        private static void PrintModelHelp()
        {
            WriteInfoHeading("Model commands");
            Console.WriteLine("  xeno model list");
            Console.WriteLine("      Show models advertised by this client.");
            Console.WriteLine("  xeno model add --provider <provider-name>");
            Console.WriteLine("                 --name <model-name>");
            Console.WriteLine("      Add a model to one configured provider endpoint.");
            Console.WriteLine("  xeno model delete --name <model-name>");
            Console.WriteLine("                    [--provider <provider-name>]");
            Console.WriteLine("      Delete a model from one provider endpoint, or from all providers when omitted.");
        }

        private static void WriteSuccess(string message)
        {
            WriteColored(message, ConsoleColor.Green);
        }

        private static void WriteInfo(string message)
        {
            WriteColored(message, ConsoleColor.Cyan);
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
            Console.WriteLine("  xeno inventory");
            Console.WriteLine("  xeno connect");
            WritePlainHeading("Configuration:");
            Console.WriteLine("  xeno config show");
            Console.WriteLine("  xeno config colors on");
            Console.WriteLine("  xeno config colors off");
            Console.WriteLine("  xeno server set-url <websocket-url>");
            Console.WriteLine("  xeno server set-rest-url <rest-url>");
            WritePlainHeading("Authentication:");
            Console.WriteLine("  xeno login -u <username>");
            Console.WriteLine("             [--tenant <tenant-id>]");
            Console.WriteLine("             [--server <rest-url>]");
            Console.WriteLine("  xeno set-auth --access-key <key>");
            Console.WriteLine("                --secret-key <secret>");
            Console.WriteLine("  xeno set-auth --clear");
            WritePlainHeading("Providers:");
            Console.WriteLine("  xeno provider list");
            Console.WriteLine("  xeno provider test --name <name>");
            Console.WriteLine("  xeno provider delete --name <name>");
            Console.WriteLine("  xeno provider add --name <name>");
            Console.WriteLine("                    --provider <provider>");
            Console.WriteLine("                    --endpoint <url>");
            Console.WriteLine("                    [--credential <value>]");
            WritePlainHeading("Models:");
            Console.WriteLine("  xeno model list");
            Console.WriteLine("  xeno model add --provider <provider-name>");
            Console.WriteLine("                 --name <model-name>");
            Console.WriteLine("  xeno model delete --name <model-name>");
            Console.WriteLine("                    [--provider <provider-name>]");
            WritePlainHeading("Global options:");
            Console.WriteLine("  --no-color    Disable colored output for this command.");
            Console.WriteLine("  --color       Enable colored output for this command.");
            Console.WriteLine("  --json        Emit machine-readable JSON for supported commands.");
            WritePlainHeading("Reset:");
            Console.WriteLine("  xeno reset");
            Console.WriteLine("  xeno reset --force");
            Console.WriteLine();
            Console.WriteLine("Use 'xeno login' when you have a username/password. Use 'xeno set-auth' when you already have an access key/secret.");
        }
    }
}

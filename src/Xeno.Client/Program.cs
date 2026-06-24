namespace Xeno.Client
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Http;
    using System.Net.WebSockets;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Helpers;
    using Xeno.Core.Serialization;

    /// <summary>
    /// Xeno client CLI entry point.
    /// </summary>
    public static class Program
    {
        private const string ClientVersion = "0.1.0";
        private static bool _UseColors = true;
        private static bool _UseJson = false;

        /// <summary>
        /// Run the Xeno client CLI.
        /// </summary>
        /// <param name="args">Arguments.</param>
        /// <returns>Exit code.</returns>
        public static async Task<int> Main(string[] args)
        {
            try
            {
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
                catch (JsonException ex)
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
            catch (JsonException ex)
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

        private static int HandleConfig(string[] args, ClientSettings settings)
        {
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

            WriteError("Usage: xeno config show | colors on|off");
            return 1;
        }

        private static int HandleServer(string[] args, ClientSettings settings)
        {
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

            WriteError("Usage: xeno server set-url <url> | set-rest-url <url>");
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
                WriteError("Usage: xeno login -u <username> [--tenant <tenant-id>] [--server <rest-url>]");
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

                    using (JsonDocument document = JsonDocument.Parse(responseBody))
                    {
                        JsonElement root = document.RootElement;
                        string? accessKey = TryGetString(root, "accessKey");
                        string? secretKey = TryGetString(root, "secretKey");
                        string? returnedTenantId = TryGetString(root, "tenantId");
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
            }

            WriteSuccess("Login succeeded. Stored a tenant-bound client credential in " + ClientSettings.SettingsFile + ".");
            return 0;
        }

        private static async Task<int> HandleRunnerAsync(string[] args, ClientSettings settings)
        {
            if (args.Length >= 2 && args[1] == "list")
            {
                PrintProviders(settings);
                return 0;
            }

            if (args.Length >= 4 && args[1] == "test")
            {
                string runnerName = String.Empty;
                for (int i = 2; i < args.Length - 1; i += 2)
                {
                    if (args[i] == "--name") runnerName = args[i + 1];
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

            if (args.Length >= 8 && args[1] == "add")
            {
                ClientRunnerSettings runner = new ClientRunnerSettings();
                for (int i = 2; i < args.Length - 1; i += 2)
                {
                    if (args[i] == "--name") runner.Name = args[i + 1];
                    if (args[i] == "--provider") runner.Provider = args[i + 1];
                    if (args[i] == "--endpoint") runner.Endpoint = args[i + 1];
                    if (args[i] == "--credential") runner.Credential = args[i + 1];
                }

                if (String.IsNullOrWhiteSpace(runner.Name) || String.IsNullOrWhiteSpace(runner.Provider) || String.IsNullOrWhiteSpace(runner.Endpoint))
                {
                    WriteError("Provider endpoint name, provider, and endpoint URL are required.");
                    return 1;
                }

                settings.Runners.Add(runner);
                settings.Save();
                WriteSuccess("Added provider endpoint '" + runner.Name + "'.");
                return 0;
            }

            WriteError("Usage: xeno provider list | test --name <name> | add --name <name> --provider <provider> --endpoint <url> [--credential <value>]");
            return 1;
        }

        private static int HandleModel(string[] args, ClientSettings settings)
        {
            if (args.Length >= 2 && args[1] == "list")
            {
                PrintModels(settings);
                return 0;
            }

            if (args.Length >= 6 && args[1] == "add")
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
                    WriteError("Provider endpoint name and model name are required.");
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

            WriteError("Usage: xeno model list | add --provider <name> --name <model>");
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
            while (!token.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                using (CancellationTokenSource receiveTimeout = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    receiveTimeout.CancelAfter(TimeSpan.FromSeconds(15));
                    try
                    {
                        WebSocketReceiveResult result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), receiveTimeout.Token).ConfigureAwait(false);
                        if (result.MessageType == WebSocketMessageType.Close) return;

                        string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        ClientConnectionState.MarkServerMessage(settings);
                        PrintServerMessage(message);
                        await HandleServerMessageAsync(socket, settings, message, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (!token.IsCancellationRequested)
                    {
                        await SendMessageAsync(socket, new { type = "client.heartbeat", clientId = settings.ClientId, tenantId = settings.TenantId, sentUtc = DateTime.UtcNow }, token).ConfigureAwait(false);
                        ClientConnectionState.MarkHeartbeat(settings);
                    }
                }
            }
        }

        private static async Task HandleServerMessageAsync(ClientWebSocket socket, ClientSettings settings, string message, CancellationToken token)
        {
            if (message.IndexOf("\"server.request\"", StringComparison.OrdinalIgnoreCase) < 0) return;

            object response = await ExecuteServerRequestAsync(settings, message, token).ConfigureAwait(false);
            await SendMessageAsync(socket, response, token).ConfigureAwait(false);
        }

        private static async Task<object> ExecuteServerRequestAsync(ClientSettings settings, string message, CancellationToken token)
        {
            using (JsonDocument document = JsonDocument.Parse(message))
            {
                JsonElement root = document.RootElement;
                string requestId = TryGetString(root, "requestId") ?? Guid.NewGuid().ToString("N");
                string? sourceName = TryGetString(root, "source") ?? TryGetString(root, "runnerName");
                ClientRunnerSettings? runner = String.IsNullOrWhiteSpace(sourceName)
                    ? (settings.Runners.Count > 0 ? settings.Runners[0] : null)
                    : settings.Runners.Find(item => String.Equals(item.Name, sourceName, StringComparison.OrdinalIgnoreCase));

                if (runner == null)
                {
                    return new { type = "client.response", requestId, clientId = settings.ClientId, tenantId = settings.TenantId, success = false, error = "No configured provider endpoint is available." };
                }

                string relativePath = TryGetString(root, "relativePath") ?? TryGetString(root, "path") ?? "/v1/chat/completions";
                string method = TryGetString(root, "method") ?? "POST";
                string body = "{}";
                if (root.TryGetProperty("body", out JsonElement bodyElement))
                {
                    body = bodyElement.ValueKind == JsonValueKind.String ? bodyElement.GetString() ?? "{}" : bodyElement.GetRawText();
                }

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

                    HttpResponseMessage result = await httpClient.SendAsync(request, token).ConfigureAwait(false);
                    string content = await result.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                    return new
                    {
                        type = "client.response",
                        requestId,
                        clientId = settings.ClientId,
                        tenantId = settings.TenantId,
                        success = result.IsSuccessStatusCode,
                        statusCode = (int)result.StatusCode,
                        contentType = result.Content.Headers.ContentType?.ToString(),
                        body = content
                    };
                }
            }
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
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, token).ConfigureAwait(false);
        }

        private static void WriteJson(object value)
        {
            Console.WriteLine(JsonUtil.Serialize(value, true));
        }

        private static string? TryGetString(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out JsonElement property)) return null;
            if (property.ValueKind == JsonValueKind.Null || property.ValueKind == JsonValueKind.Undefined) return null;
            return property.ToString();
        }

        private static string[] ApplyGlobalOptions(string[] args, ClientSettings settings)
        {
            _UseColors = settings.UseColors;
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
            WriteInfo("Xeno client configuration");
            Console.WriteLine("  File:       " + ClientSettings.SettingsFile);
            Console.WriteLine("  Name:       " + settings.Name);
            Console.WriteLine("  Client ID:  " + DisplayValue(settings.ClientId, "(created on first connect)"));
            Console.WriteLine("  Tenant:     " + settings.TenantId);
            Console.WriteLine("  Colors:     " + FormatBool(settings.UseColors));
            Console.WriteLine();
            Console.WriteLine("Server");
            Console.WriteLine("  WebSocket:  " + settings.Server.Url);
            Console.WriteLine("  REST:       " + DisplayValue(settings.Server.RestUrl, "(derived from WebSocket URL)"));
            Console.WriteLine("  Access key: " + DisplayValue(settings.Server.AccessKey, "(not set)"));
            Console.WriteLine("  Secret key: " + MaskSecret(settings.Server.SecretKey));
            Console.WriteLine("  REST token: " + MaskSecret(settings.Server.Token));
            Console.WriteLine();
            PrintProviders(settings);
        }

        private static void PrintInventory(ClientSettings settings)
        {
            WriteInfo("Advertised client inventory");
            Console.WriteLine("  Machine:    " + Environment.MachineName);
            Console.WriteLine("  OS:         " + Environment.OSVersion);
            Console.WriteLine("  Arch:       " + System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture);
            Console.WriteLine("  Processors: " + Environment.ProcessorCount);
            Console.WriteLine("  Memory:     " + FormatBytes(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes));
            Console.WriteLine("  Version:    " + ClientVersion);
            Console.WriteLine();
            PrintProviders(settings);
            Console.WriteLine();
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
                using (JsonDocument document = JsonDocument.Parse(message))
                {
                    JsonElement root = document.RootElement;
                    string? type = TryGetString(root, "type");
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
                        string requestId = TryGetString(root, "requestId") ?? "(no request ID)";
                        string method = TryGetString(root, "method") ?? "POST";
                        string path = TryGetString(root, "relativePath") ?? TryGetString(root, "path") ?? "/v1/chat/completions";
                        string source = TryGetString(root, "source") ?? TryGetString(root, "runnerName") ?? "(default provider)";
                        WriteInfo("Received request " + requestId + ": " + method + " " + path + " via " + source + ".");
                        return;
                    }

                    if (String.Equals(type, "server.error", StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(type, "server.unauthorized", StringComparison.OrdinalIgnoreCase))
                    {
                        WriteError(TryGetString(root, "message") ?? "The Xeno server rejected the client message.");
                        return;
                    }

                    if (!String.IsNullOrWhiteSpace(type))
                    {
                        WriteInfo("Server message: " + type + ".");
                        return;
                    }
                }
            }
            catch (JsonException)
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

            WriteInfo("Xeno client status");
            Console.WriteLine("  Name:       " + settings.Name);
            Console.WriteLine("  Client ID:  " + (String.IsNullOrWhiteSpace(settings.ClientId) ? "(created on first connect)" : settings.ClientId));
            Console.WriteLine("  Tenant:     " + settings.TenantId);
            Console.WriteLine("  WebSocket:  " + settings.Server.Url);
            Console.WriteLine("  REST:       " + DisplayValue(settings.Server.RestUrl, "(derived from WebSocket URL)"));
            Console.WriteLine("  Auth:       " + DescribeAuth(settings));
            Console.WriteLine("  Providers:  " + settings.Runners.Count);
            Console.WriteLine("  Colors:     " + FormatBool(settings.UseColors));
            Console.WriteLine();
            Console.WriteLine("Connection");
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
                WriteInfo("No provider endpoints are configured.");
                return;
            }

            WriteInfo("Configured provider endpoints");
            foreach (ClientRunnerSettings runner in settings.Runners)
            {
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
                    if (!any) WriteInfo("Configured models");
                    any = true;
                    Console.WriteLine("  - " + model.Name);
                    Console.WriteLine("    Provider:    " + runner.Name + " [" + runner.Provider + "]");
                    Console.WriteLine("    Completions: " + FormatBool(model.SupportsCompletions));
                    Console.WriteLine("    Embeddings:  " + FormatBool(model.SupportsEmbeddings));
                }
            }

            if (!any) WriteInfo("No models are configured.");
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
                using (JsonDocument document = JsonDocument.Parse(responseBody))
                {
                    JsonElement root = document.RootElement;
                    return TryGetString(root, "message")
                        ?? TryGetString(root, "error")
                        ?? TryGetString(root, "title")
                        ?? TryGetString(root, "detail");
                }
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static void PrintAuthHelp()
        {
            WriteInfo("Authentication commands");
            Console.WriteLine("  xeno login -u <username> [--tenant <tenant-id>] [--server <rest-url>]");
            Console.WriteLine("      Prompt for a password and store only the returned access key/secret.");
            Console.WriteLine("  xeno set-auth --access-key <key> --secret-key <secret>");
            Console.WriteLine("      Store an existing client credential for WebSocket and API use.");
            Console.WriteLine("  xeno set-auth --clear");
            Console.WriteLine("      Remove stored authentication.");
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
            WriteInfo("xeno " + ClientVersion);
            Console.WriteLine("Usage:");
            Console.WriteLine("  xeno init");
            Console.WriteLine("  xeno config show");
            Console.WriteLine("  xeno config colors on|off");
            Console.WriteLine("  xeno server set-url <url>");
            Console.WriteLine("  xeno server set-rest-url <url>");
            Console.WriteLine("  xeno login -u <username> [--tenant <tenant-id>] [--server <rest-url>]");
            Console.WriteLine("  xeno set-auth --access-key <key> --secret-key <secret>");
            Console.WriteLine("  xeno provider add --name <name> --provider <provider> --endpoint <url> [--credential <value>]");
            Console.WriteLine("  xeno provider test --name <name>");
            Console.WriteLine("  xeno provider list");
            Console.WriteLine("  xeno model add --provider <name> --name <model>");
            Console.WriteLine("  xeno model list");
            Console.WriteLine("  xeno inventory");
            Console.WriteLine("  xeno connect");
            Console.WriteLine("  xeno status");
            Console.WriteLine();
            Console.WriteLine("Global options:");
            Console.WriteLine("  --no-color    Disable colored output for this command.");
            Console.WriteLine("  --color       Enable colored output for this command.");
            Console.WriteLine("  --json        Emit machine-readable JSON for supported commands.");
            Console.WriteLine();
            Console.WriteLine("Use 'xeno login' when you have a username/password. Use 'xeno set-auth' when you already have an access key/secret.");
        }
    }
}

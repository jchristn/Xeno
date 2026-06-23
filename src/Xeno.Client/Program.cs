namespace Xeno.Client
{
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Net.WebSockets;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Serialization;

    /// <summary>
    /// Xeno client CLI entry point.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Run the Xeno client CLI.
        /// </summary>
        /// <param name="args">Arguments.</param>
        /// <returns>Exit code.</returns>
        public static async Task<int> Main(string[] args)
        {
            if (args.Length == 0 || args[0] == "help" || args[0] == "--help" || args[0] == "-h")
            {
                PrintHelp();
                return 0;
            }

            ClientSettings settings = ClientSettings.Load();
            string command = args[0].ToLowerInvariant();

            switch (command)
            {
                case "version":
                    Console.WriteLine("xeno 0.1.0");
                    return 0;
                case "init":
                    settings.Save();
                    Console.WriteLine("Created " + ClientSettings.SettingsFile);
                    return 0;
                case "config":
                    return HandleConfig(args, settings);
                case "server":
                    return HandleServer(args, settings);
                case "auth":
                    return HandleAuth(args, settings);
                case "runner":
                    return HandleRunner(args, settings);
                case "model":
                    return HandleModel(args, settings);
                case "inventory":
                    Console.WriteLine(JsonUtil.Serialize(settings.BuildInventory(), true));
                    return 0;
                case "status":
                    Console.WriteLine("Client: " + settings.Name);
                    Console.WriteLine("Server: " + settings.Server.Url);
                    Console.WriteLine("Runners: " + settings.Runners.Count);
                    return 0;
                case "connect":
                    await ConnectAsync(settings, CancellationToken.None).ConfigureAwait(false);
                    return 0;
                default:
                    Console.WriteLine("Unknown command '" + args[0] + "'.");
                    PrintHelp();
                    return 1;
            }
        }

        private static int HandleConfig(string[] args, ClientSettings settings)
        {
            if (args.Length >= 2 && args[1] == "show")
            {
                Console.WriteLine(JsonUtil.Serialize(settings, true));
                return 0;
            }

            Console.WriteLine("Usage: xeno config show");
            return 1;
        }

        private static int HandleServer(string[] args, ClientSettings settings)
        {
            if (args.Length >= 3 && args[1] == "set-url")
            {
                settings.Server.Url = args[2];
                settings.Save();
                return 0;
            }

            Console.WriteLine("Usage: xeno server set-url <url>");
            return 1;
        }

        private static int HandleAuth(string[] args, ClientSettings settings)
        {
            if (args.Length >= 3 && args[1] == "set-token")
            {
                settings.Server.Token = args[2];
                settings.Save();
                return 0;
            }

            if (args.Length >= 4 && args[1] == "set-credential")
            {
                settings.Server.AccessKey = args[2];
                settings.Server.SecretKey = args[3];
                settings.Save();
                return 0;
            }

            Console.WriteLine("Usage: xeno auth set-token <token> | set-credential <access-key> <secret-key>");
            return 1;
        }

        private static int HandleRunner(string[] args, ClientSettings settings)
        {
            if (args.Length >= 2 && args[1] == "list")
            {
                Console.WriteLine(JsonUtil.Serialize(settings.Runners, true));
                return 0;
            }

            if (args.Length >= 8 && args[1] == "add")
            {
                ClientRunnerSettings runner = new ClientRunnerSettings();
                for (int i = 2; i < args.Length - 1; i += 2)
                {
                    if (args[i] == "--name") runner.Name = args[i + 1];
                    if (args[i] == "--provider") runner.Provider = args[i + 1];
                    if (args[i] == "--endpoint") runner.Endpoint = args[i + 1];
                }
                settings.Runners.Add(runner);
                settings.Save();
                return 0;
            }

            Console.WriteLine("Usage: xeno runner list | add --name <name> --provider <provider> --endpoint <url>");
            return 1;
        }

        private static int HandleModel(string[] args, ClientSettings settings)
        {
            if (args.Length >= 2 && args[1] == "list")
            {
                Console.WriteLine(JsonUtil.Serialize(settings.Runners, true));
                return 0;
            }

            if (args.Length >= 6 && args[1] == "add")
            {
                string runnerName = String.Empty;
                string modelName = String.Empty;
                for (int i = 2; i < args.Length - 1; i += 2)
                {
                    if (args[i] == "--runner") runnerName = args[i + 1];
                    if (args[i] == "--name") modelName = args[i + 1];
                }

                ClientRunnerSettings? runner = settings.Runners.Find(item => String.Equals(item.Name, runnerName, StringComparison.OrdinalIgnoreCase));
                if (runner == null)
                {
                    Console.WriteLine("Runner not found.");
                    return 1;
                }

                runner.Models.Add(new ClientModelSettings { Name = modelName });
                settings.Save();
                return 0;
            }

            Console.WriteLine("Usage: xeno model list | add --runner <name> --name <model>");
            return 1;
        }

        private static async Task ConnectAsync(ClientSettings settings, CancellationToken token)
        {
            if (String.IsNullOrWhiteSpace(settings.ClientId))
            {
                settings.ClientId = "xcl_" + Guid.NewGuid().ToString("N");
                settings.Save();
            }

            while (!token.IsCancellationRequested)
            {
                try
                {
                    using (ClientWebSocket socket = new ClientWebSocket())
                    {
                        await socket.ConnectAsync(new Uri(settings.Server.Url), token).ConfigureAwait(false);
                        await SendMessageAsync(socket, new { type = "client.register", clientId = settings.ClientId, tenantId = settings.TenantId, name = settings.Name, inventory = settings.BuildInventory() }, token).ConfigureAwait(false);
                        await ReceiveLoopAsync(socket, settings, token).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Disconnected from Xeno server: " + ex.Message);
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
                        Console.WriteLine(message);
                        await HandleServerMessageAsync(socket, settings, message, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (!token.IsCancellationRequested)
                    {
                        await SendMessageAsync(socket, new { type = "client.heartbeat", clientId = settings.ClientId, tenantId = settings.TenantId, sentUtc = DateTime.UtcNow }, token).ConfigureAwait(false);
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
                string? runnerName = TryGetString(root, "runnerName");
                ClientRunnerSettings? runner = String.IsNullOrWhiteSpace(runnerName)
                    ? (settings.Runners.Count > 0 ? settings.Runners[0] : null)
                    : settings.Runners.Find(item => String.Equals(item.Name, runnerName, StringComparison.OrdinalIgnoreCase));

                if (runner == null)
                {
                    return new { type = "client.response", requestId, clientId = settings.ClientId, tenantId = settings.TenantId, success = false, error = "No configured runner is available." };
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

        private static async Task SendMessageAsync(ClientWebSocket socket, object value, CancellationToken token)
        {
            string payload = JsonUtil.Serialize(value);
            byte[] bytes = Encoding.UTF8.GetBytes(payload);
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, token).ConfigureAwait(false);
        }

        private static string? TryGetString(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out JsonElement property)) return null;
            if (property.ValueKind == JsonValueKind.Null || property.ValueKind == JsonValueKind.Undefined) return null;
            return property.ToString();
        }

        private static void PrintHelp()
        {
            Console.WriteLine("xeno 0.1.0");
            Console.WriteLine("Usage:");
            Console.WriteLine("  xeno init");
            Console.WriteLine("  xeno config show");
            Console.WriteLine("  xeno server set-url <url>");
            Console.WriteLine("  xeno auth set-token <token>");
            Console.WriteLine("  xeno auth set-credential <access-key> <secret-key>");
            Console.WriteLine("  xeno runner add --name <name> --provider <provider> --endpoint <url>");
            Console.WriteLine("  xeno runner list");
            Console.WriteLine("  xeno model add --runner <name> --name <model>");
            Console.WriteLine("  xeno model list");
            Console.WriteLine("  xeno inventory");
            Console.WriteLine("  xeno connect");
            Console.WriteLine("  xeno status");
        }
    }
}

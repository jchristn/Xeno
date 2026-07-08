namespace Xeno.Sdk.Console
{
    using System;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Sdk;

    internal static class Program
    {
        private static string _BaseUrl = "http://localhost:9000";
        private static string? _TenantId;
        private static string? _BearerToken = "default";

        private static async Task<int> Main(string[] args)
        {
            if (args.Length > 0) _BaseUrl = args[0];

            bool run = true;
            while (run)
            {
                System.Console.Write("xeno-sdk [? for help]> ");
                string? line = System.Console.ReadLine();
                if (line == null) break;

                string input = line.Trim();
                if (String.IsNullOrWhiteSpace(input)) continue;

                try
                {
                    if (input.Equals("?", StringComparison.OrdinalIgnoreCase) || input.Equals("help", StringComparison.OrdinalIgnoreCase)) PrintHelp();
                    else if (input.Equals("q", StringComparison.OrdinalIgnoreCase) || input.Equals("quit", StringComparison.OrdinalIgnoreCase)) run = false;
                    else if (input.Equals("cls", StringComparison.OrdinalIgnoreCase)) System.Console.Clear();
                    else if (input.StartsWith("url ", StringComparison.OrdinalIgnoreCase)) _BaseUrl = input.Substring(4).Trim();
                    else if (input.StartsWith("tenant ", StringComparison.OrdinalIgnoreCase)) _TenantId = input.Substring(7).Trim();
                    else if (input.StartsWith("token ", StringComparison.OrdinalIgnoreCase)) _BearerToken = input.Substring(6).Trim();
                    else await RunCommandAsync(input, CancellationToken.None).ConfigureAwait(false);
                }
                catch (XenoApiException ex)
                {
                    WriteError("API " + (int)ex.StatusCode + " at " + ex.Endpoint + ": " + ex.Message);
                }
                catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
                {
                    WriteError(ex.Message);
                }
            }

            return 0;
        }

        private static async Task RunCommandAsync(string command, CancellationToken token)
        {
            using (XenoClient client = new XenoClient(_BaseUrl, _BearerToken, _TenantId))
            {
                if (command.Equals("health", StringComparison.OrdinalIgnoreCase)) await PrintJsonAsync(await client.GetHealthAsync(token).ConfigureAwait(false)).ConfigureAwait(false);
                else if (command.Equals("models", StringComparison.OrdinalIgnoreCase)) await PrintJsonAsync(await client.ListOpenAiModelsAsync(token).ConfigureAwait(false)).ConfigureAwait(false);
                else if (command.Equals("clients", StringComparison.OrdinalIgnoreCase)) await PrintJsonAsync(await client.ListClientsAsync(token).ConfigureAwait(false)).ConfigureAwait(false);
                else if (command.Equals("tenants", StringComparison.OrdinalIgnoreCase)) await PrintJsonAsync(await client.ListTenantsAsync(token).ConfigureAwait(false)).ConfigureAwait(false);
                else if (command.Equals("users", StringComparison.OrdinalIgnoreCase)) await PrintJsonAsync(await client.ListUsersAsync(token).ConfigureAwait(false)).ConfigureAwait(false);
                else if (command.Equals("credentials", StringComparison.OrdinalIgnoreCase)) await PrintJsonAsync(await client.ListCredentialsAsync(token).ConfigureAwait(false)).ConfigureAwait(false);
                else if (command.Equals("roles", StringComparison.OrdinalIgnoreCase)) await PrintJsonAsync(await client.ListRolesAsync(token).ConfigureAwait(false)).ConfigureAwait(false);
                else if (command.Equals("permissions", StringComparison.OrdinalIgnoreCase)) await PrintJsonAsync(await client.ListPermissionsAsync(token).ConfigureAwait(false)).ConfigureAwait(false);
                else if (command.Equals("routing-policies", StringComparison.OrdinalIgnoreCase)) await PrintJsonAsync(await client.ListRoutingPoliciesAsync(token).ConfigureAwait(false)).ConfigureAwait(false);
                else if (command.Equals("history", StringComparison.OrdinalIgnoreCase)) await PrintJsonAsync(await client.ListRequestHistoryAsync(token: token).ConfigureAwait(false)).ConfigureAwait(false);
                else if (command.Equals("leaderboard", StringComparison.OrdinalIgnoreCase)) await PrintJsonAsync(await client.GetModelTokenLeaderboardAsync(token: token).ConfigureAwait(false)).ConfigureAwait(false);
                else if (command.Equals("settings", StringComparison.OrdinalIgnoreCase)) await PrintJsonAsync(await client.GetSettingsAsync(token).ConfigureAwait(false)).ConfigureAwait(false);
                else if (command.Equals("metrics", StringComparison.OrdinalIgnoreCase)) System.Console.WriteLine(await client.GetMetricsAsync(token).ConfigureAwait(false));
                else if (command.Equals("login", StringComparison.OrdinalIgnoreCase)) await LoginAsync(client, token).ConfigureAwait(false);
                else if (command.Equals("chat", StringComparison.OrdinalIgnoreCase)) await ChatAsync(client, token).ConfigureAwait(false);
                else WriteError("Unknown command. Type ? for help.");
            }
        }

        private static async Task LoginAsync(XenoClient client, CancellationToken token)
        {
            string username = Prompt("Username", "admin@xeno");
            string password = Prompt("Password", "password");
            CredentialLoginResponse response = await client.LoginAsync(username, password, _TenantId ?? "ten_default", token).ConfigureAwait(false);
            await PrintJsonAsync(response).ConfigureAwait(false);
        }

        private static async Task ChatAsync(XenoClient client, CancellationToken token)
        {
            string model = Prompt("Model", "llama3.2:latest");
            string prompt = Prompt("Prompt", "hello");
            OpenAiChatCompletion response = await client.CreateChatCompletionAsync(new
            {
                model,
                messages = new[]
                {
                    new { role = "user", content = prompt }
                }
            }, token).ConfigureAwait(false);
            await PrintJsonAsync(response).ConfigureAwait(false);
        }

        private static string Prompt(string label, string defaultValue)
        {
            System.Console.Write(label + " [" + defaultValue + "]: ");
            string value = System.Console.ReadLine() ?? String.Empty;
            return String.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
        }

        private static Task PrintJsonAsync<T>(T response)
        {
            System.Console.WriteLine(JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true }));
            return Task.CompletedTask;
        }

        private static void PrintHelp()
        {
            System.Console.WriteLine("");
            System.Console.WriteLine("Connection");
            System.Console.WriteLine("  url <url>       set server URL, current " + _BaseUrl);
            System.Console.WriteLine("  tenant <id>     set tenant header, current " + (_TenantId ?? "<inferred>"));
            System.Console.WriteLine("  token <token>   set bearer token, current " + (_BearerToken ?? "<none>"));
            System.Console.WriteLine("");
            System.Console.WriteLine("Read commands");
            System.Console.WriteLine("  health          server health");
            System.Console.WriteLine("  models          OpenAI-compatible model list");
            System.Console.WriteLine("  clients         connected/registered clients");
            System.Console.WriteLine("  tenants         tenants");
            System.Console.WriteLine("  users           users");
            System.Console.WriteLine("  credentials     credentials");
            System.Console.WriteLine("  roles           roles");
            System.Console.WriteLine("  permissions     permissions");
            System.Console.WriteLine("  routing-policies routing policies");
            System.Console.WriteLine("  history         request history");
            System.Console.WriteLine("  leaderboard     public model token leaderboard");
            System.Console.WriteLine("  settings        server settings");
            System.Console.WriteLine("  metrics         Prometheus metrics");
            System.Console.WriteLine("");
            System.Console.WriteLine("Actions");
            System.Console.WriteLine("  login           exchange username/password for a client credential");
            System.Console.WriteLine("  chat            send a chat completion request");
            System.Console.WriteLine("");
            System.Console.WriteLine("Other");
            System.Console.WriteLine("  cls             clear screen");
            System.Console.WriteLine("  q               quit");
            System.Console.WriteLine("");
        }

        private static void WriteError(string message)
        {
            ConsoleColor original = System.Console.ForegroundColor;
            System.Console.ForegroundColor = ConsoleColor.Red;
            System.Console.WriteLine("Error: " + message);
            System.Console.ForegroundColor = original;
        }
    }
}

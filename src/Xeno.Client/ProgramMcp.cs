namespace Xeno.Client
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Voltaic.Mcp;
    using Xeno.Client.Chat;
    using Xeno.Core.Chat;
    using Xeno.Client.Services;
    using Xeno.Client.Tools;

    /// <summary>
    /// The "xeno mcp" command.
    /// </summary>
    public static partial class Program
    {
        private static async Task<int> HandleMcpAsync(string[] args)
        {
            string sub = args.Length > 1 ? args[1].ToLowerInvariant() : "stdio";
            switch (sub)
            {
                case "stdio":
                    return await RunMcpStdioAsync().ConfigureAwait(false);
                case "tools":
                    return PrintMcpTools();
                case "token":
                    return PrintMcpToken(args.Skip(2).Any(arg => String.Equals(arg, "--rotate", StringComparison.OrdinalIgnoreCase)));
                case "config":
                    return PrintMcpConfig();
                case "help":
                case "-h":
                case "--help":
                case "/?":
                    PrintMcpHelp();
                    return 0;
                default:
                    Console.Error.WriteLine("Unknown mcp command '" + args[1] + "'.");
                    PrintMcpHelp();
                    return 1;
            }
        }

        private static async Task<int> RunMcpStdioAsync()
        {
            // Stdout carries MCP messages; diagnostics go to stderr only.
            ClientSettings settings = ClientSettings.Load();
            using (XenoWorkspace workspace = new XenoWorkspace())
            using (XenoChatClient chat = new XenoChatClient())
            using (CancellationTokenSource cancellation = new CancellationTokenSource())
            {
                SqliteChatStore? store = null;
                SemaphoreSlim storeLock = new SemaphoreSlim(1, 1);
                async Task<IChatStore> OpenStoreAsync(CancellationToken token)
                {
                    await storeLock.WaitAsync(token).ConfigureAwait(false);
                    try
                    {
                        if (store == null)
                        {
                            SqliteChatStore opened = new SqliteChatStore(settings.LocalDashboard.Database);
                            await opened.InitializeAsync(token).ConfigureAwait(false);
                            store = opened;
                        }

                        return store;
                    }
                    finally
                    {
                        storeLock.Release();
                    }
                }

                XenoToolCatalog catalog = new XenoToolCatalog(workspace, OpenStoreAsync, new XenoApiChatGateway(chat));
                McpServer server = new McpServer();
                XenoMcpTools.Register(server, catalog);
                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true;
                    cancellation.Cancel();
                };
                await server.RunAsync(cancellation.Token).ConfigureAwait(false);
                storeLock.Dispose();
                return 0;
            }
        }

        private static int PrintMcpTools()
        {
            using (XenoWorkspace workspace = new XenoWorkspace())
            using (XenoChatClient chat = new XenoChatClient())
            {
                XenoToolCatalog catalog = new XenoToolCatalog(workspace, token => throw new InvalidOperationException("Listing only."), new XenoApiChatGateway(chat));
                Console.WriteLine("Xeno MCP tools (read-only tools run without asking in Xeno chat; the others ask first):");
                Console.WriteLine();
                foreach (XenoTool tool in catalog.Tools)
                {
                    Console.WriteLine("  " + tool.Name.PadRight(28) + (tool.Mutating ? "changes things  " : "read-only       ") + tool.Description);
                }

                return 0;
            }
        }

        private static int PrintMcpToken(bool rotate)
        {
            string token = rotate ? XenoMcpToken.Rotate() : XenoMcpToken.GetOrCreate();
            Console.WriteLine(token);
            if (rotate) Console.Error.WriteLine("Replaced the MCP token. Update MCP clients that use the HTTP server.");
            return 0;
        }

        private static int PrintMcpConfig()
        {
            ClientSettings settings = ClientSettings.Load();
            ClientMcpSettings mcp = settings.Mcp ?? new ClientMcpSettings();
            string executable = Environment.ProcessPath ?? "xeno";
            Console.WriteLine("Stdio (works whenever this machine has Xeno; xeno_chat needs the client running):");
            Console.WriteLine();
            Console.WriteLine("  claude mcp add xeno -- \"" + executable + "\" mcp");
            Console.WriteLine();
            Console.WriteLine("  {\"mcpServers\": {\"xeno\": {\"command\": \"" + executable.Replace("\\", "\\\\", StringComparison.Ordinal) + "\", \"args\": [\"mcp\"]}}}");
            Console.WriteLine();
            Console.WriteLine("HTTP (" + (mcp.Enabled ? "enabled" : "off; turn on Mcp.Enabled in Settings, then restart the client") + "):");
            Console.WriteLine();
            string url = mcp.Url().Replace("/rpc", "/mcp", StringComparison.Ordinal);
            if (mcp.RequireToken)
            {
                Console.WriteLine("  claude mcp add --transport http xeno " + url + " --header \"Authorization: Bearer $(xeno mcp token)\"");
            }
            else
            {
                Console.WriteLine("  claude mcp add --transport http xeno " + url);
            }

            return 0;
        }

        private static void PrintMcpHelp()
        {
            Console.WriteLine("Usage: xeno mcp [stdio|tools|config|token [--rotate]]");
            Console.WriteLine();
            Console.WriteLine("Xeno's MCP server gives agents such as Claude Code this machine's Xeno tools: status, models here");
            Console.WriteLine("and on the Xeno network, routing, providers, request and chat history, feedback, and xeno_chat.");
            Console.WriteLine();
            Console.WriteLine("  xeno mcp           Serve over stdio. Add it to an MCP client as the command \"xeno mcp\".");
            Console.WriteLine("  xeno mcp tools     List the tools.");
            Console.WriteLine("  xeno mcp config    Show how to add Xeno to Claude Code (stdio and HTTP).");
            Console.WriteLine("  xeno mcp token     Show the token for the HTTP server (port 11436 when enabled); --rotate replaces it.");
        }
    }
}

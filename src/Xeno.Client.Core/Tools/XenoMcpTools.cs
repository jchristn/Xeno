namespace Xeno.Client.Tools
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Voltaic.Core;
    using Voltaic.Mcp;

    /// <summary>
    /// Registers the Xeno tools on Voltaic MCP servers, so the HTTP server in the client process and the stdio server
    /// behind "xeno mcp" publish the same tools.
    /// </summary>
    public static class XenoMcpTools
    {
        /// <summary>
        /// Server name reported to MCP clients.
        /// </summary>
        public const string ServerName = "Xeno";

        /// <summary>
        /// Instructions reported to MCP clients.
        /// </summary>
        public const string Instructions = "Tools for this machine's Xeno client: status, models on this machine and the Xeno network, routing, providers, request and chat history, feedback, and xeno_chat to ask any of those models a question.";

        /// <summary>
        /// Register every tool on a stdio server.
        /// </summary>
        /// <param name="server">Server.</param>
        /// <param name="catalog">Catalog.</param>
        /// <exception cref="ArgumentNullException">server or catalog is null.</exception>
        public static void Register(McpServer server, XenoToolCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(server);
            ArgumentNullException.ThrowIfNull(catalog);
            server.ServerName = ServerName;
            server.ServerVersion = Xeno.Client.Hosting.XenoProduct.Version;
            server.IncludeToolExceptionMessages = true;
            foreach (XenoTool tool in catalog.Tools)
            {
                XenoTool current = tool;
                server.RegisterTool(current.Name, current.Description, current.Schema, (RpcParameters? parameters, CancellationToken token) => InvokeAsync(current, parameters, token));
            }
        }

        /// <summary>
        /// Register every tool on an HTTP server.
        /// </summary>
        /// <param name="server">Server.</param>
        /// <param name="catalog">Catalog.</param>
        /// <exception cref="ArgumentNullException">server or catalog is null.</exception>
        public static void Register(McpHttpServer server, XenoToolCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(server);
            ArgumentNullException.ThrowIfNull(catalog);
            server.ServerName = ServerName;
            server.ServerVersion = Xeno.Client.Hosting.XenoProduct.Version;
            server.ServerInstructions = Instructions;
            server.IncludeToolExceptionMessages = true;
            foreach (XenoTool tool in catalog.Tools)
            {
                XenoTool current = tool;
                server.RegisterTool(current.Name, current.Description, current.Schema, (RpcParameters? parameters, CancellationToken token) => InvokeAsync(current, parameters, token));
            }
        }

        private static async Task<object> InvokeAsync(XenoTool tool, RpcParameters? parameters, CancellationToken token)
        {
            string json = parameters != null && parameters.HasValue && !String.IsNullOrWhiteSpace(parameters.RawJson) ? parameters.RawJson : "{}";
            return await tool.Invoke(json, token).ConfigureAwait(false);
        }
    }
}

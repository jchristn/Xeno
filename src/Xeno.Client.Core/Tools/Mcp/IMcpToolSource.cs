namespace Xeno.Client.Tools.Mcp
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Tools from external MCP servers configured in <see cref="ClientChatSettings.McpServers"/>.
    /// </summary>
    public interface IMcpToolSource
    {
        /// <summary>
        /// Tools from every enabled server that connected, named mcp_&lt;server&gt;_&lt;tool&gt;. Never blocks on a
        /// connection; servers still starting or that failed are left out.
        /// </summary>
        IReadOnlyList<McpChatTool> Tools { get; }

        /// <summary>
        /// Connect to enabled servers that are not connected yet and refresh their tool lists, picking up settings
        /// changes. Failures are kept per server in <see cref="Servers"/>.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task RefreshAsync(CancellationToken token);

        /// <summary>
        /// Connection state of each configured server.
        /// </summary>
        IReadOnlyList<McpServerStatus> Servers { get; }

        /// <summary>
        /// Call a tool.
        /// </summary>
        /// <param name="name">Tool name as offered (mcp_&lt;server&gt;_&lt;tool&gt;).</param>
        /// <param name="arguments">Arguments as JSON text.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The tool's result content as text (text parts joined; other parts described).</returns>
        /// <exception cref="System.ArgumentException">There is no such tool.</exception>
        /// <exception cref="System.InvalidOperationException">The server is not connected or the call failed.</exception>
        Task<string> InvokeAsync(string name, string arguments, CancellationToken token);
    }
}

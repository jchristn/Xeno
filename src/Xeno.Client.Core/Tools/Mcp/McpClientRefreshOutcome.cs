namespace Xeno.Client.Tools.Mcp
{
    using System;

    /// <summary>
    /// What refreshing one server produced: a connection, or why there is none.
    /// </summary>
    internal sealed class McpClientRefreshOutcome
    {
        /// <summary>
        /// Server name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Connection, or null.
        /// </summary>
        public McpClientConnection? Connection { get; set; } = null;

        /// <summary>
        /// Why there is no connection, or null.
        /// </summary>
        public string? Error { get; set; } = null;
    }
}

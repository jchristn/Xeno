namespace Xeno.Client.Tools.Mcp
{
    using System;

    /// <summary>
    /// Connection state of one external MCP server.
    /// </summary>
    public sealed class McpServerStatus
    {
        /// <summary>
        /// Server name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// "stdio" or "http".
        /// </summary>
        public string Transport { get; set; } = "stdio";

        /// <summary>
        /// Enabled in settings.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Connected and listed its tools.
        /// </summary>
        public bool Connected { get; set; } = false;

        /// <summary>
        /// Tools the server offers.
        /// </summary>
        public int ToolCount { get; set; } = 0;

        /// <summary>
        /// Why the server is not connected, or null.
        /// </summary>
        public string? Error { get; set; } = null;
    }
}

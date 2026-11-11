namespace Xeno.Client
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// One external MCP server whose tools chat may use: a local command over stdio, or a URL over streamable HTTP.
    /// </summary>
    public class ChatMcpServerSettings
    {
        private int _TimeoutSeconds = 60;

        /// <summary>
        /// Name, unique among servers; tools are offered to the model as mcp_&lt;name&gt;_&lt;tool&gt;.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Offer this server's tools.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// "stdio" (run <see cref="Command"/>) or "http" (connect to <see cref="Url"/>).
        /// </summary>
        public string Transport { get; set; } = "stdio";

        /// <summary>
        /// Executable for stdio servers, for example npx or uvx.
        /// </summary>
        public string? Command { get; set; } = null;

        /// <summary>
        /// Arguments for stdio servers.
        /// </summary>
        public List<string> Arguments { get; set; } = new List<string>();

        /// <summary>
        /// Extra environment variables for stdio servers.
        /// </summary>
        public Dictionary<string, string> Environment { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// Working directory for stdio servers, or null for the client home.
        /// </summary>
        public string? WorkingDirectory { get; set; } = null;

        /// <summary>
        /// Endpoint for http servers.
        /// </summary>
        public string? Url { get; set; } = null;

        /// <summary>
        /// Extra headers for http servers, for example Authorization.
        /// </summary>
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Treat every tool from this server as read-only, so calls run without asking. Off by default: calls ask for
        /// Allow or Deny unless the server marks a tool read-only.
        /// </summary>
        public bool TrustReadOnlyTools { get; set; } = false;

        /// <summary>
        /// Seconds to wait for the server to start or answer one call, 5 to 600.
        /// </summary>
        public int TimeoutSeconds
        {
            get { return _TimeoutSeconds; }
            set { _TimeoutSeconds = Math.Clamp(value, 5, 600); }
        }
    }
}

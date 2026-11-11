namespace Xeno.Client
{
    using System;

    /// <summary>
    /// Settings for the client's MCP server over HTTP. "xeno mcp" (stdio) works whether or not this is enabled.
    /// </summary>
    public class ClientMcpSettings
    {
        /// <summary>
        /// Default port. The local inference endpoint uses 11435 and Ollama 11434.
        /// </summary>
        public const int DefaultPort = 11436;

        private int _Port = DefaultPort;

        /// <summary>
        /// True to serve MCP over HTTP while the client runs. Default false.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Listen address. Loopback only: 127.0.0.1, localhost, ::1, or [::1].
        /// </summary>
        public string Host { get; set; } = "127.0.0.1";

        /// <summary>
        /// Port, 1 to 65535.
        /// </summary>
        public int Port
        {
            get { return _Port; }
            set { _Port = Math.Clamp(value, 1, 65535); }
        }

        /// <summary>
        /// True to require the MCP token (Authorization: Bearer, from ~/.xeno/mcp-token). Default true.
        /// </summary>
        public bool RequireToken { get; set; } = true;

        /// <summary>
        /// The MCP endpoint URL for these settings.
        /// </summary>
        /// <returns>URL ending in /rpc.</returns>
        public string Url()
        {
            string host = Host.Contains(':', StringComparison.Ordinal) && !Host.StartsWith("[", StringComparison.Ordinal) ? "[" + Host + "]" : Host;
            return "http://" + host + ":" + Port.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/rpc";
        }
    }
}

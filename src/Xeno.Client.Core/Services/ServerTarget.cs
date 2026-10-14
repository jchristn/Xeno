namespace Xeno.Client.Services
{
    using System;

    /// <summary>
    /// A resolved Xeno server connection: the WebSocket URL the worker connects to and the REST URL the apps call.
    /// </summary>
    public class ServerTarget
    {
        /// <summary>
        /// WebSocket URL, for example wss://app.xenocloud.ai:9000/ws/v1/clients.
        /// </summary>
        public string WebSocketUrl { get; set; } = String.Empty;

        /// <summary>
        /// Explicit REST URL, or null when the REST URL is derived from the WebSocket URL.
        /// </summary>
        public string? RestUrl { get; set; } = null;

        /// <summary>
        /// True when this target is the Xeno cloud server.
        /// </summary>
        public bool IsCloud { get; set; } = false;

        /// <summary>
        /// Short label for display: "Xeno cloud", or host and port.
        /// </summary>
        public string Label { get; set; } = String.Empty;
    }
}

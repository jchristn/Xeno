namespace Xeno.Client
{
    using System;
    /// <summary>
    /// Client server connection settings.
    /// </summary>
    public class ClientServerSettings
    {
        /// <summary>
        /// Server URL.
        /// </summary>
        public string Url { get; set; } = "ws://app.xenocloud.ai:9000/ws/v1/clients";

        /// <summary>
        /// REST API base URL.
        /// </summary>
        public string? RestUrl { get; set; } = null;

        /// <summary>
        /// Access key.
        /// </summary>
        public string? AccessKey { get; set; } = null;

        /// <summary>
        /// Legacy secret key. This value is not sent to the server.
        /// </summary>
        public string? SecretKey { get; set; } = null;

        /// <summary>
        /// Bearer token.
        /// </summary>
        public string? Token { get; set; } = null;

        /// <summary>
        /// Client-to-server heartbeat interval in milliseconds. Default is 15000. Values below 1000 are clamped to 1000.
        /// </summary>
        public int HeartbeatIntervalMs
        {
            get
            {
                return _HeartbeatIntervalMs;
            }
            set
            {
                _HeartbeatIntervalMs = Math.Clamp(value, 1000, 300000);
            }
        }

        /// <summary>
        /// Maximum time in milliseconds without a server WebSocket message before the client reconnects. Default is 60000. Values below 5000 are clamped to 5000.
        /// </summary>
        public int LivenessTimeoutMs
        {
            get
            {
                return _LivenessTimeoutMs;
            }
            set
            {
                _LivenessTimeoutMs = Math.Clamp(value, 5000, 600000);
            }
        }

        /// <summary>
        /// Initial reconnect delay in milliseconds. Default is 1000. Values below 0 are clamped to 0.
        /// </summary>
        public int ReconnectInitialDelayMs
        {
            get
            {
                return _ReconnectInitialDelayMs;
            }
            set
            {
                _ReconnectInitialDelayMs = Math.Clamp(value, 0, 600000);
            }
        }

        /// <summary>
        /// Maximum reconnect delay in milliseconds. Default is 60000. Values below 1000 are clamped to 1000.
        /// </summary>
        public int ReconnectMaxDelayMs
        {
            get
            {
                return _ReconnectMaxDelayMs;
            }
            set
            {
                _ReconnectMaxDelayMs = Math.Clamp(value, 1000, 600000);
            }
        }

        /// <summary>
        /// Maximum random reconnect jitter in milliseconds. Default is 1000. Values below 0 are clamped to 0.
        /// </summary>
        public int ReconnectJitterMs
        {
            get
            {
                return _ReconnectJitterMs;
            }
            set
            {
                _ReconnectJitterMs = Math.Clamp(value, 0, 60000);
            }
        }

        private int _HeartbeatIntervalMs = 15000;
        private int _LivenessTimeoutMs = 60000;
        private int _ReconnectInitialDelayMs = 1000;
        private int _ReconnectMaxDelayMs = 60000;
        private int _ReconnectJitterMs = 1000;
    }
}

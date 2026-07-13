namespace Xeno.Core.Settings
{
    using System;
    using System.Collections.Generic;
    using Xeno.Core.Enums;

    /// <summary>
    /// WebSocket listener settings.
    /// </summary>
    public class WebSocketSettings
    {
        /// <summary>
        /// Enable WebSocket listener.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// WebSocket path.
        /// </summary>
        public string Path { get; set; } = "/ws/v1/clients";

        /// <summary>
        /// Heartbeat interval in milliseconds.
        /// </summary>
        public int HeartbeatIntervalMs { get; set; } = 15000;

        /// <summary>
        /// Timeout for server-to-client inference requests in milliseconds.
        /// </summary>
        public int RequestTimeoutMs { get; set; } = 120000;

        /// <summary>
        /// Maximum time in milliseconds without a client heartbeat before a WebSocket client session is considered stale. Default is 60000. Values below 5000 are clamped to 5000.
        /// </summary>
        public int ClientStaleTimeoutMs
        {
            get
            {
                return _ClientStaleTimeoutMs;
            }
            set
            {
                _ClientStaleTimeoutMs = Math.Clamp(value, 5000, 600000);
            }
        }

        /// <summary>
        /// Interval in milliseconds for scanning and evicting stale WebSocket client sessions. Default is 5000. Values below 1000 are clamped to 1000.
        /// </summary>
        public int StaleConnectionScanIntervalMs
        {
            get
            {
                return _StaleConnectionScanIntervalMs;
            }
            set
            {
                _StaleConnectionScanIntervalMs = Math.Clamp(value, 1000, 600000);
            }
        }

        private int _ClientStaleTimeoutMs = 60000;
        private int _StaleConnectionScanIntervalMs = 5000;
    }
}

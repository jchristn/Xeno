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
    }
}

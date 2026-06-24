namespace Xeno.Client
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using Xeno.Core.Serialization;

    /// <summary>
    /// Persisted runtime connection state for the CLI.
    /// </summary>
    public class ClientConnectionState
    {
        /// <summary>
        /// Runtime state file path.
        /// </summary>
        public static string StateFile
        {
            get
            {
                return Path.Combine(ClientSettings.SettingsDirectory, "state.json");
            }
        }

        /// <summary>
        /// Whether the client is currently connected.
        /// </summary>
        public bool IsConnected { get; set; } = false;

        /// <summary>
        /// Client process ID that last wrote a connected state.
        /// </summary>
        public int? ProcessId { get; set; } = null;

        /// <summary>
        /// Client ID.
        /// </summary>
        public string? ClientId { get; set; } = null;

        /// <summary>
        /// Tenant ID.
        /// </summary>
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// WebSocket URL.
        /// </summary>
        public string? WebSocketUrl { get; set; } = null;

        /// <summary>
        /// Last connection timestamp.
        /// </summary>
        public DateTime? ConnectedUtc { get; set; } = null;

        /// <summary>
        /// Last disconnection timestamp.
        /// </summary>
        public DateTime? DisconnectedUtc { get; set; } = null;

        /// <summary>
        /// Last heartbeat timestamp.
        /// </summary>
        public DateTime? LastHeartbeatUtc { get; set; } = null;

        /// <summary>
        /// Last received server message timestamp.
        /// </summary>
        public DateTime? LastServerMessageUtc { get; set; } = null;

        /// <summary>
        /// Last state update timestamp.
        /// </summary>
        public DateTime? LastUpdatedUtc { get; set; } = null;

        /// <summary>
        /// Last disconnection reason.
        /// </summary>
        public string? LastDisconnectReason { get; set; } = null;

        /// <summary>
        /// Load connection state from disk.
        /// </summary>
        /// <returns>Connection state.</returns>
        public static ClientConnectionState Load()
        {
            if (!File.Exists(StateFile)) return new ClientConnectionState();

            try
            {
                ClientConnectionState state = JsonUtil.Deserialize<ClientConnectionState>(File.ReadAllText(StateFile)) ?? new ClientConnectionState();
                if (state.IsConnected && !state.IsProcessRunning())
                {
                    state.IsConnected = false;
                    state.DisconnectedUtc = DateTime.UtcNow;
                    state.LastDisconnectReason = "Client process is no longer running.";
                    state.LastUpdatedUtc = state.DisconnectedUtc;
                    state.Save();
                }

                return state;
            }
            catch
            {
                return new ClientConnectionState();
            }
        }

        /// <summary>
        /// Save connection state to disk.
        /// </summary>
        public void Save()
        {
            Directory.CreateDirectory(ClientSettings.SettingsDirectory);
            File.WriteAllText(StateFile, JsonUtil.Serialize(this, true));
        }

        /// <summary>
        /// Mark the client connected.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        public static void MarkConnected(ClientSettings settings)
        {
            DateTime now = DateTime.UtcNow;
            ClientConnectionState state = new ClientConnectionState
            {
                IsConnected = true,
                ProcessId = Environment.ProcessId,
                ClientId = settings.ClientId,
                TenantId = settings.TenantId,
                WebSocketUrl = settings.Server.Url,
                ConnectedUtc = now,
                DisconnectedUtc = null,
                LastHeartbeatUtc = null,
                LastServerMessageUtc = null,
                LastUpdatedUtc = now,
                LastDisconnectReason = null
            };
            state.Save();
        }

        /// <summary>
        /// Mark the latest heartbeat.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        public static void MarkHeartbeat(ClientSettings settings)
        {
            ClientConnectionState state = Load();
            DateTime now = DateTime.UtcNow;
            state.IsConnected = true;
            state.ProcessId = Environment.ProcessId;
            state.ClientId = settings.ClientId;
            state.TenantId = settings.TenantId;
            state.WebSocketUrl = settings.Server.Url;
            if (state.ConnectedUtc == null) state.ConnectedUtc = now;
            state.LastHeartbeatUtc = now;
            state.LastUpdatedUtc = now;
            state.LastDisconnectReason = null;
            state.Save();
        }

        /// <summary>
        /// Mark the latest server message.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        public static void MarkServerMessage(ClientSettings settings)
        {
            ClientConnectionState state = Load();
            DateTime now = DateTime.UtcNow;
            state.IsConnected = true;
            state.ProcessId = Environment.ProcessId;
            state.ClientId = settings.ClientId;
            state.TenantId = settings.TenantId;
            state.WebSocketUrl = settings.Server.Url;
            if (state.ConnectedUtc == null) state.ConnectedUtc = now;
            state.LastServerMessageUtc = now;
            state.LastUpdatedUtc = now;
            state.LastDisconnectReason = null;
            state.Save();
        }

        /// <summary>
        /// Mark the client disconnected.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="reason">Disconnect reason.</param>
        public static void MarkDisconnected(ClientSettings settings, string reason)
        {
            ClientConnectionState state = Load();
            DateTime now = DateTime.UtcNow;
            state.IsConnected = false;
            state.ProcessId = Environment.ProcessId;
            state.ClientId = settings.ClientId;
            state.TenantId = settings.TenantId;
            state.WebSocketUrl = settings.Server.Url;
            if (state.ConnectedUtc == null) state.ConnectedUtc = now;
            state.DisconnectedUtc = now;
            state.LastUpdatedUtc = now;
            state.LastDisconnectReason = reason;
            state.Save();
        }

        private bool IsProcessRunning()
        {
            if (ProcessId == null) return false;

            try
            {
                using (Process process = Process.GetProcessById(ProcessId.Value))
                {
                    return !process.HasExited;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}

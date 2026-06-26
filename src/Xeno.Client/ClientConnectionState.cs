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
        /// Runtime stop request file path.
        /// </summary>
        public static string StopFile
        {
            get
            {
                return Path.Combine(ClientSettings.SettingsDirectory, "stop");
            }
        }

        /// <summary>
        /// Runtime log directory.
        /// </summary>
        public static string LogDirectory
        {
            get
            {
                return Path.Combine(ClientSettings.SettingsDirectory, "logs");
            }
        }

        /// <summary>
        /// Runtime standard output log path.
        /// </summary>
        public static string LogFile
        {
            get
            {
                return Path.Combine(LogDirectory, "xeno.log");
            }
        }

        /// <summary>
        /// Runtime standard error log path.
        /// </summary>
        public static string ErrorLogFile
        {
            get
            {
                return Path.Combine(LogDirectory, "xeno.err.log");
            }
        }

        /// <summary>
        /// Whether the client is currently connected and registered.
        /// </summary>
        public bool IsConnected { get; set; } = false;

        /// <summary>
        /// Whether the client has received server registration acknowledgement.
        /// </summary>
        public bool IsRegistered { get; set; } = false;

        /// <summary>
        /// Whether the process is running as a background worker.
        /// </summary>
        public bool IsBackground { get; set; } = false;

        /// <summary>
        /// Client process ID that last wrote a connected state.
        /// </summary>
        public int? ProcessId { get; set; } = null;

        /// <summary>
        /// Executable path that started the process.
        /// </summary>
        public string? ExecutablePath { get; set; } = null;

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
        /// Last WebSocket connection timestamp before registration.
        /// </summary>
        public DateTime? WebSocketConnectedUtc { get; set; } = null;

        /// <summary>
        /// Last registration acknowledgement timestamp.
        /// </summary>
        public DateTime? RegisteredUtc { get; set; } = null;

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
        /// Process start timestamp.
        /// </summary>
        public DateTime? StartedUtc { get; set; } = null;

        /// <summary>
        /// Last stop request timestamp.
        /// </summary>
        public DateTime? StopRequestedUtc { get; set; } = null;

        /// <summary>
        /// Last disconnection reason.
        /// </summary>
        public string? LastDisconnectReason { get; set; } = null;

        /// <summary>
        /// Standard output log path.
        /// </summary>
        public string? LogFilePath { get; set; } = null;

        /// <summary>
        /// Standard error log path.
        /// </summary>
        public string? ErrorLogFilePath { get; set; } = null;

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
                if ((state.IsConnected || state.IsRegistered) && !state.IsProcessRunning())
                {
                    state.IsConnected = false;
                    state.IsRegistered = false;
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
            string tempFile = StateFile + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(tempFile, JsonUtil.Serialize(this, true));
            File.Move(tempFile, StateFile, true);
        }

        /// <summary>
        /// Mark the client process as started.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="isBackground">Whether the process is a background worker.</param>
        public static void MarkStarted(ClientSettings settings, bool isBackground)
        {
            DateTime now = DateTime.UtcNow;
            ClientConnectionState state = new ClientConnectionState
            {
                IsConnected = false,
                IsRegistered = false,
                IsBackground = isBackground,
                ProcessId = Environment.ProcessId,
                ExecutablePath = Environment.ProcessPath,
                ClientId = settings.ClientId,
                TenantId = settings.TenantId,
                WebSocketUrl = settings.Server.Url,
                StartedUtc = now,
                ConnectedUtc = null,
                DisconnectedUtc = null,
                LastHeartbeatUtc = null,
                LastServerMessageUtc = null,
                LastUpdatedUtc = now,
                LogFilePath = LogFile,
                ErrorLogFilePath = ErrorLogFile,
                LastDisconnectReason = null
            };
            state.Save();
        }

        /// <summary>
        /// Mark the WebSocket connected before registration is complete.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        public static void MarkWebSocketConnected(ClientSettings settings)
        {
            ClientConnectionState state = Load();
            DateTime now = DateTime.UtcNow;
            state.IsConnected = false;
            state.IsRegistered = false;
            state.ProcessId = Environment.ProcessId;
            state.ExecutablePath = Environment.ProcessPath;
            state.ClientId = settings.ClientId;
            state.TenantId = settings.TenantId;
            state.WebSocketUrl = settings.Server.Url;
            if (state.StartedUtc == null) state.StartedUtc = now;
            state.WebSocketConnectedUtc = now;
            state.LastUpdatedUtc = now;
            state.LastDisconnectReason = null;
            state.LogFilePath = LogFile;
            state.ErrorLogFilePath = ErrorLogFile;
            state.Save();
        }

        /// <summary>
        /// Mark the client registered and fully connected.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        public static void MarkRegistered(ClientSettings settings)
        {
            ClientConnectionState state = Load();
            DateTime now = DateTime.UtcNow;
            state.IsConnected = true;
            state.IsRegistered = true;
            state.ProcessId = Environment.ProcessId;
            state.ExecutablePath = Environment.ProcessPath;
            state.ClientId = settings.ClientId;
            state.TenantId = settings.TenantId;
            state.WebSocketUrl = settings.Server.Url;
            if (state.StartedUtc == null) state.StartedUtc = now;
            if (state.WebSocketConnectedUtc == null) state.WebSocketConnectedUtc = now;
            state.ConnectedUtc = now;
            state.RegisteredUtc = now;
            state.DisconnectedUtc = null;
            state.LastUpdatedUtc = now;
            state.LastDisconnectReason = null;
            state.LogFilePath = LogFile;
            state.ErrorLogFilePath = ErrorLogFile;
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
            if (state.IsRegistered) state.IsConnected = true;
            state.ProcessId = Environment.ProcessId;
            state.ExecutablePath = Environment.ProcessPath;
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
            if (state.IsRegistered) state.IsConnected = true;
            state.ProcessId = Environment.ProcessId;
            state.ExecutablePath = Environment.ProcessPath;
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
            state.IsRegistered = false;
            state.ProcessId = Environment.ProcessId;
            state.ExecutablePath = Environment.ProcessPath;
            state.ClientId = settings.ClientId;
            state.TenantId = settings.TenantId;
            state.WebSocketUrl = settings.Server.Url;
            state.DisconnectedUtc = now;
            state.LastUpdatedUtc = now;
            state.LastDisconnectReason = reason;
            state.Save();
        }

        /// <summary>
        /// Request the background client to stop.
        /// </summary>
        public static void RequestStop()
        {
            Directory.CreateDirectory(ClientSettings.SettingsDirectory);
            File.WriteAllText(StopFile, DateTime.UtcNow.ToString("O"));
            ClientConnectionState state = Load();
            state.StopRequestedUtc = DateTime.UtcNow;
            state.LastUpdatedUtc = state.StopRequestedUtc;
            state.Save();
        }

        /// <summary>
        /// Clear a pending stop request.
        /// </summary>
        public static void ClearStopRequest()
        {
            if (File.Exists(StopFile)) File.Delete(StopFile);
        }

        /// <summary>
        /// Determine whether a stop has been requested.
        /// </summary>
        /// <returns>True if a stop request is pending.</returns>
        public static bool IsStopRequested()
        {
            return File.Exists(StopFile);
        }

        /// <summary>
        /// Determine whether the recorded process is still running.
        /// </summary>
        /// <returns>True if the process is running.</returns>
        public bool IsRecordedProcessRunning()
        {
            return IsProcessRunning();
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

namespace Xeno.Client
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using Xeno.Client.Services;
    using Xeno.Core.Serialization;

    /// <summary>
    /// Persisted runtime connection state for the CLI.
    /// </summary>
    public class ClientConnectionState
    {
        private static readonly TimeSpan _MinimumActivityStateSaveInterval = TimeSpan.FromSeconds(1);
        private static readonly object _ActivityStateSync = new object();
        private static readonly Dictionary<string, DateTime> _LastActivityStateSaveUtc = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

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
        /// Whether the local dashboard is enabled.
        /// </summary>
        public bool LocalDashboardEnabled { get; set; } = false;

        /// <summary>
        /// Whether the local dashboard is listening.
        /// </summary>
        public bool LocalDashboardListening { get; set; } = false;

        /// <summary>
        /// Local dashboard URL.
        /// </summary>
        public string? LocalDashboardUrl { get; set; } = null;

        /// <summary>
        /// Local dashboard host.
        /// </summary>
        public string? LocalDashboardHost { get; set; } = null;

        /// <summary>
        /// Local dashboard port.
        /// </summary>
        public int? LocalDashboardPort { get; set; } = null;

        /// <summary>
        /// Local dashboard last started timestamp.
        /// </summary>
        public DateTime? LocalDashboardStartedUtc { get; set; } = null;

        /// <summary>
        /// Local dashboard last stopped timestamp.
        /// </summary>
        public DateTime? LocalDashboardStoppedUtc { get; set; } = null;

        /// <summary>
        /// Local dashboard last error.
        /// </summary>
        public string? LocalDashboardLastError { get; set; } = null;

        /// <summary>
        /// Whether the local inference server is enabled.
        /// </summary>
        public bool LocalInferenceServerEnabled { get; set; } = false;

        /// <summary>
        /// Whether the local inference server is listening.
        /// </summary>
        public bool LocalInferenceServerListening { get; set; } = false;

        /// <summary>
        /// Local inference server URL.
        /// </summary>
        public string? LocalInferenceServerUrl { get; set; } = null;

        /// <summary>
        /// Local inference server host.
        /// </summary>
        public string? LocalInferenceServerHost { get; set; } = null;

        /// <summary>
        /// Local inference server port.
        /// </summary>
        public int? LocalInferenceServerPort { get; set; } = null;

        /// <summary>
        /// Local inference server last started timestamp.
        /// </summary>
        public DateTime? LocalInferenceServerStartedUtc { get; set; } = null;

        /// <summary>
        /// Local inference server last stopped timestamp.
        /// </summary>
        public DateTime? LocalInferenceServerStoppedUtc { get; set; } = null;

        /// <summary>
        /// Local inference server last request timestamp.
        /// </summary>
        public DateTime? LocalInferenceServerLastRequestUtc { get; set; } = null;

        /// <summary>
        /// Local inference server last error.
        /// </summary>
        public string? LocalInferenceServerLastError { get; set; } = null;

        /// <summary>
        /// Listener port the client migrated away from (the pre-0.2.0 default). Null when no migration notice is pending.
        /// </summary>
        public int? LocalInferencePortMigratedFrom { get; set; } = null;

        /// <summary>
        /// Listener port the client migrated to. Null when no migration notice is pending.
        /// </summary>
        public int? LocalInferencePortMigratedTo { get; set; } = null;

        /// <summary>
        /// Last inventory update request timestamp.
        /// </summary>
        public DateTime? InventoryUpdateRequestedUtc { get; set; } = null;

        /// <summary>
        /// Last inventory update sent timestamp.
        /// </summary>
        public DateTime? InventoryUpdateSentUtc { get; set; } = null;

        /// <summary>
        /// Load connection state from disk.
        /// </summary>
        /// <returns>Connection state.</returns>
        public static ClientConnectionState Load()
        {
            if (!File.Exists(StateFile)) return new ClientConnectionState();

            try
            {
                ClientConnectionState state = JsonUtil.Deserialize<ClientConnectionState>(ClientFileIo.ReadAllText(StateFile)) ?? new ClientConnectionState();
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
            ClientFileIo.WriteAllTextAtomic(StateFile, JsonUtil.Serialize(this, true));
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
            DateTime now = DateTime.UtcNow;
            if (!ShouldSaveActivityState("server:" + StateFile, now)) return;

            ClientConnectionState state = Load();
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
        /// Mark local dashboard disabled.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        public static void MarkLocalDashboardDisabled(ClientSettings settings)
        {
            ClientConnectionState state = Load();
            ApplyLocalDashboardSettings(state, settings);
            state.LocalDashboardEnabled = false;
            state.LocalDashboardListening = false;
            state.LocalDashboardUrl = null;
            state.LocalDashboardLastError = null;
            state.LastUpdatedUtc = DateTime.UtcNow;
            state.Save();
        }

        /// <summary>
        /// Mark local dashboard starting.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        public static void MarkLocalDashboardStarting(ClientSettings settings)
        {
            ClientConnectionState state = Load();
            ApplyLocalDashboardSettings(state, settings);
            state.LocalDashboardEnabled = true;
            state.LocalDashboardListening = false;
            state.LocalDashboardLastError = null;
            state.LastUpdatedUtc = DateTime.UtcNow;
            state.Save();
        }

        /// <summary>
        /// Mark local dashboard listening.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="url">Dashboard URL.</param>
        public static void MarkLocalDashboardListening(ClientSettings settings, string url)
        {
            ClientConnectionState state = Load();
            ApplyLocalDashboardSettings(state, settings);
            state.LocalDashboardEnabled = true;
            state.LocalDashboardListening = true;
            state.LocalDashboardUrl = url;
            state.LocalDashboardStartedUtc = DateTime.UtcNow;
            state.LocalDashboardLastError = null;
            state.LastUpdatedUtc = state.LocalDashboardStartedUtc;
            state.Save();
        }

        /// <summary>
        /// Mark local dashboard error.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="error">Error.</param>
        public static void MarkLocalDashboardError(ClientSettings settings, string error)
        {
            ClientConnectionState state = Load();
            ApplyLocalDashboardSettings(state, settings);
            state.LocalDashboardEnabled = settings.LocalDashboard.Enabled;
            state.LocalDashboardListening = false;
            state.LocalDashboardLastError = error;
            state.LastUpdatedUtc = DateTime.UtcNow;
            state.Save();
        }

        /// <summary>
        /// Mark local dashboard stopped.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        public static void MarkLocalDashboardStopped(ClientSettings settings)
        {
            ClientConnectionState state = Load();
            ApplyLocalDashboardSettings(state, settings);
            state.LocalDashboardEnabled = settings.LocalDashboard.Enabled;
            state.LocalDashboardListening = false;
            state.LocalDashboardStoppedUtc = DateTime.UtcNow;
            state.LastUpdatedUtc = state.LocalDashboardStoppedUtc;
            state.Save();
        }

        /// <summary>
        /// Mark local inference server disabled.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        public static void MarkLocalInferenceDisabled(ClientSettings settings)
        {
            ClientConnectionState state = Load();
            ApplyLocalInferenceSettings(state, settings);
            state.LocalInferenceServerEnabled = false;
            state.LocalInferenceServerListening = false;
            state.LocalInferenceServerUrl = null;
            state.LocalInferenceServerLastError = null;
            state.LastUpdatedUtc = DateTime.UtcNow;
            state.Save();
        }

        /// <summary>
        /// Mark local inference server starting.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        public static void MarkLocalInferenceStarting(ClientSettings settings)
        {
            ClientConnectionState state = Load();
            ApplyLocalInferenceSettings(state, settings);
            state.LocalInferenceServerEnabled = true;
            state.LocalInferenceServerListening = false;
            state.LocalInferenceServerLastError = null;
            state.LastUpdatedUtc = DateTime.UtcNow;
            state.Save();
        }

        /// <summary>
        /// Mark local inference server listening.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="url">Listener URL.</param>
        public static void MarkLocalInferenceListening(ClientSettings settings, string url)
        {
            ClientConnectionState state = Load();
            ApplyLocalInferenceSettings(state, settings);
            state.LocalInferenceServerEnabled = true;
            state.LocalInferenceServerListening = true;
            state.LocalInferenceServerUrl = url;
            state.LocalInferenceServerStartedUtc = DateTime.UtcNow;
            state.LocalInferenceServerLastError = null;
            state.LastUpdatedUtc = state.LocalInferenceServerStartedUtc;
            state.Save();
        }

        /// <summary>
        /// Mark local inference server error.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="error">Error.</param>
        public static void MarkLocalInferenceError(ClientSettings settings, string error)
        {
            ClientConnectionState state = Load();
            ApplyLocalInferenceSettings(state, settings);
            state.LocalInferenceServerEnabled = settings.LocalInferenceServer.Enabled;
            state.LocalInferenceServerListening = false;
            state.LocalInferenceServerLastError = error;
            state.LastUpdatedUtc = DateTime.UtcNow;
            state.Save();
        }

        /// <summary>
        /// Mark local inference server stopped.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        public static void MarkLocalInferenceStopped(ClientSettings settings)
        {
            ClientConnectionState state = Load();
            ApplyLocalInferenceSettings(state, settings);
            state.LocalInferenceServerEnabled = settings.LocalInferenceServer.Enabled;
            state.LocalInferenceServerListening = false;
            state.LocalInferenceServerStoppedUtc = DateTime.UtcNow;
            state.LastUpdatedUtc = state.LocalInferenceServerStoppedUtc;
            state.Save();
        }

        /// <summary>
        /// Mark local inference server request.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        public static void MarkLocalInferenceRequest(ClientSettings settings)
        {
            DateTime now = DateTime.UtcNow;
            if (!ShouldSaveActivityState("local-inference:" + StateFile, now)) return;

            ClientConnectionState state = Load();
            ApplyLocalInferenceSettings(state, settings);
            state.LocalInferenceServerLastRequestUtc = now;
            state.LastUpdatedUtc = state.LocalInferenceServerLastRequestUtc;
            state.Save();
        }

        /// <summary>
        /// Record that the local inference listener port was migrated, so front ends can show a one-time notice.
        /// </summary>
        /// <param name="fromPort">Port migrated away from.</param>
        /// <param name="toPort">Port migrated to.</param>
        public static void RecordLocalInferencePortMigration(int fromPort, int toPort)
        {
            ClientConnectionState state = Load();
            state.LocalInferencePortMigratedFrom = fromPort;
            state.LocalInferencePortMigratedTo = toPort;
            state.LastUpdatedUtc = DateTime.UtcNow;
            state.Save();
        }

        /// <summary>
        /// Clear the local inference port migration notice after a front end has shown it.
        /// </summary>
        public static void AcknowledgeLocalInferencePortMigration()
        {
            ClientConnectionState state = Load();
            if (!state.LocalInferencePortMigratedFrom.HasValue && !state.LocalInferencePortMigratedTo.HasValue) return;
            state.LocalInferencePortMigratedFrom = null;
            state.LocalInferencePortMigratedTo = null;
            state.LastUpdatedUtc = DateTime.UtcNow;
            state.Save();
        }

        /// <summary>
        /// Mark inventory update requested.
        /// </summary>
        public static void MarkInventoryUpdateRequested()
        {
            ClientConnectionState state = Load();
            state.InventoryUpdateRequestedUtc = DateTime.UtcNow;
            state.LastUpdatedUtc = state.InventoryUpdateRequestedUtc;
            state.Save();
        }

        /// <summary>
        /// Mark inventory update sent.
        /// </summary>
        public static void MarkInventoryUpdateSent()
        {
            ClientConnectionState state = Load();
            state.InventoryUpdateSentUtc = DateTime.UtcNow;
            state.LastUpdatedUtc = state.InventoryUpdateSentUtc;
            state.Save();
        }

        /// <summary>
        /// Determine if inventory update is pending.
        /// </summary>
        /// <returns>True if pending.</returns>
        public static bool IsInventoryUpdatePending()
        {
            ClientConnectionState state = Load();
            if (state.InventoryUpdateRequestedUtc == null) return false;
            if (state.InventoryUpdateSentUtc == null) return true;
            return state.InventoryUpdateRequestedUtc > state.InventoryUpdateSentUtc;
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
        /// Determine whether the recorded connection has recent server or heartbeat activity.
        /// </summary>
        /// <param name="utcNow">Current UTC timestamp.</param>
        /// <param name="livenessTimeoutMs">Maximum activity age in milliseconds.</param>
        /// <returns>True if the latest activity is within the timeout window.</returns>
        public bool HasFreshActivity(DateTime utcNow, int livenessTimeoutMs)
        {
            DateTime? lastActivityUtc = LastServerMessageUtc ?? LastHeartbeatUtc ?? RegisteredUtc ?? ConnectedUtc ?? WebSocketConnectedUtc;
            if (lastActivityUtc == null) return false;
            TimeSpan age = utcNow - lastActivityUtc.Value;
            return age <= TimeSpan.FromMilliseconds(Math.Max(5000, livenessTimeoutMs));
        }

        private static void ApplyLocalDashboardSettings(ClientConnectionState state, ClientSettings settings)
        {
            state.LocalDashboardHost = settings.LocalDashboard.Host;
            state.LocalDashboardPort = settings.LocalDashboard.Port;
        }

        private static void ApplyLocalInferenceSettings(ClientConnectionState state, ClientSettings settings)
        {
            state.LocalInferenceServerHost = settings.LocalInferenceServer.Host;
            state.LocalInferenceServerPort = settings.LocalInferenceServer.Port;
        }

        private static bool ShouldSaveActivityState(string key, DateTime now)
        {
            lock (_ActivityStateSync)
            {
                if (_LastActivityStateSaveUtc.TryGetValue(key, out DateTime lastSavedUtc) &&
                    now - lastSavedUtc < _MinimumActivityStateSaveInterval)
                {
                    return false;
                }

                _LastActivityStateSaveUtc[key] = now;
                return true;
            }
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

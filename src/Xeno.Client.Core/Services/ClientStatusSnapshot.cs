namespace Xeno.Client.Services
{
    using System;
    using System.Linq;

    /// <summary>
    /// Point-in-time view of this machine's Xeno client, combining client.json and state.json into the fields every
    /// front end displays. Capture a new snapshot to refresh; instances are not updated in place.
    /// </summary>
    public class ClientStatusSnapshot
    {
        #region Public-Members

        /// <summary>
        /// Overall status.
        /// </summary>
        public ClientStatusEnum Status { get; set; } = ClientStatusEnum.Stopped;

        /// <summary>
        /// Short status label, for example "Connected".
        /// </summary>
        public string StatusText { get; set; } = String.Empty;

        /// <summary>
        /// One-sentence explanation of the status and what to do next.
        /// </summary>
        public string StatusDetail { get; set; } = String.Empty;

        /// <summary>
        /// Client display name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Client identifier.
        /// </summary>
        public string ClientId { get; set; } = String.Empty;

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// Xeno server WebSocket URL.
        /// </summary>
        public string WebSocketUrl { get; set; } = String.Empty;

        /// <summary>
        /// Xeno server REST base URL, or an empty string when the configured URL is invalid.
        /// </summary>
        public string RestBaseUrl { get; set; } = String.Empty;

        /// <summary>
        /// Short server label: "Xeno cloud", or the server's host and port.
        /// </summary>
        public string ServerLabel { get; set; } = String.Empty;

        /// <summary>
        /// Description of the configured server authentication.
        /// </summary>
        public string AuthDescription { get; set; } = String.Empty;

        /// <summary>
        /// True when an access key is configured.
        /// </summary>
        public bool HasCredential { get; set; } = false;

        /// <summary>
        /// True when the recorded worker process is running.
        /// </summary>
        public bool Running { get; set; } = false;

        /// <summary>
        /// True when the worker reports an open server connection.
        /// </summary>
        public bool Connected { get; set; } = false;

        /// <summary>
        /// True when the server accepted the worker's registration.
        /// </summary>
        public bool Registered { get; set; } = false;

        /// <summary>
        /// Worker process identifier.
        /// </summary>
        public int? ProcessId { get; set; } = null;

        /// <summary>
        /// When the worker started (UTC).
        /// </summary>
        public DateTime? StartedUtc { get; set; } = null;

        /// <summary>
        /// When the worker registered (UTC).
        /// </summary>
        public DateTime? RegisteredUtc { get; set; } = null;

        /// <summary>
        /// Last heartbeat sent (UTC).
        /// </summary>
        public DateTime? LastHeartbeatUtc { get; set; } = null;

        /// <summary>
        /// Last message received from the server (UTC).
        /// </summary>
        public DateTime? LastServerMessageUtc { get; set; } = null;

        /// <summary>
        /// Reason for the last disconnect.
        /// </summary>
        public string LastDisconnectReason { get; set; } = String.Empty;

        /// <summary>
        /// True when the local dashboard is enabled in settings.
        /// </summary>
        public bool LocalDashboardEnabled { get; set; } = false;

        /// <summary>
        /// True when the local dashboard is listening.
        /// </summary>
        public bool LocalDashboardListening { get; set; } = false;

        /// <summary>
        /// Local dashboard URL when listening.
        /// </summary>
        public string LocalDashboardUrl { get; set; } = String.Empty;

        /// <summary>
        /// Last local dashboard error.
        /// </summary>
        public string LocalDashboardLastError { get; set; } = String.Empty;

        /// <summary>
        /// True when the local inference server is enabled in settings.
        /// </summary>
        public bool LocalInferenceEnabled { get; set; } = false;

        /// <summary>
        /// True when the local inference server is listening.
        /// </summary>
        public bool LocalInferenceListening { get; set; } = false;

        /// <summary>
        /// Local inference server URL when listening, otherwise the configured URL.
        /// </summary>
        public string LocalInferenceUrl { get; set; } = String.Empty;

        /// <summary>
        /// Last local inference server error.
        /// </summary>
        public string LocalInferenceLastError { get; set; } = String.Empty;

        /// <summary>
        /// Last request served by the local inference server (UTC).
        /// </summary>
        public DateTime? LocalInferenceLastRequestUtc { get; set; } = null;

        /// <summary>
        /// Number of configured provider endpoints.
        /// </summary>
        public int ProviderCount { get; set; } = 0;

        /// <summary>
        /// Number of configured models across providers.
        /// </summary>
        public int ModelCount { get; set; } = 0;

        /// <summary>
        /// True when a configuration change is waiting to be re-advertised by the worker.
        /// </summary>
        public bool InventoryUpdatePending { get; set; } = false;

        /// <summary>
        /// Settings file path.
        /// </summary>
        public string SettingsFile { get; set; } = String.Empty;

        /// <summary>
        /// Worker log file path.
        /// </summary>
        public string LogFile { get; set; } = String.Empty;

        /// <summary>
        /// Worker error log file path.
        /// </summary>
        public string ErrorLogFile { get; set; } = String.Empty;

        /// <summary>
        /// When the snapshot was captured (UTC).
        /// </summary>
        public DateTime CapturedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// True when the worker can be started.
        /// </summary>
        public bool CanStart
        {
            get { return !Running && HasCredential; }
        }

        /// <summary>
        /// True when the worker can be stopped.
        /// </summary>
        public bool CanStop
        {
            get { return Running; }
        }

        /// <summary>
        /// True when the local dashboard can be opened.
        /// </summary>
        public bool CanOpenLocalDashboard
        {
            get { return Running && LocalDashboardListening && !String.IsNullOrWhiteSpace(LocalDashboardUrl); }
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Capture a snapshot from client.json and state.json.
        /// </summary>
        /// <returns>Snapshot.</returns>
        public static ClientStatusSnapshot Capture()
        {
            return From(ClientSettings.Load(), ClientConnectionState.Load());
        }

        /// <summary>
        /// Build a snapshot from loaded settings and state.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="state">Connection state.</param>
        /// <returns>Snapshot.</returns>
        /// <exception cref="ArgumentNullException">Settings or state is null.</exception>
        public static ClientStatusSnapshot From(ClientSettings settings, ClientConnectionState state)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(state);
            bool running = state.ProcessId != null && state.IsRecordedProcessRunning();
            bool hasCredential = ServerEndpoints.HasWebSocketCredential(settings);
            ClientStatusSnapshot snapshot = new ClientStatusSnapshot
            {
                Name = settings.Name ?? String.Empty,
                ClientId = settings.ClientId ?? state.ClientId ?? String.Empty,
                TenantId = settings.TenantId ?? state.TenantId ?? String.Empty,
                WebSocketUrl = settings.Server.Url ?? state.WebSocketUrl ?? String.Empty,
                RestBaseUrl = ServerEndpoints.TryBuildRestBaseUrl(settings) ?? String.Empty,
                ServerLabel = ServerTargets.Label(settings.Server.Url, settings.Server.RestUrl),
                AuthDescription = ServerEndpoints.DescribeAuth(settings),
                HasCredential = hasCredential,
                Running = running,
                Connected = running && state.IsConnected,
                Registered = running && state.IsRegistered,
                ProcessId = running ? state.ProcessId : null,
                StartedUtc = running ? state.StartedUtc : null,
                RegisteredUtc = running ? state.RegisteredUtc : null,
                LastHeartbeatUtc = state.LastHeartbeatUtc,
                LastServerMessageUtc = state.LastServerMessageUtc,
                LastDisconnectReason = state.LastDisconnectReason ?? String.Empty,
                LocalDashboardEnabled = settings.LocalDashboard.Enabled,
                LocalDashboardListening = running && state.LocalDashboardListening,
                LocalDashboardUrl = running ? state.LocalDashboardUrl ?? String.Empty : String.Empty,
                LocalDashboardLastError = state.LocalDashboardLastError ?? String.Empty,
                LocalInferenceEnabled = settings.LocalInferenceServer.Enabled,
                LocalInferenceListening = running && state.LocalInferenceServerListening,
                LocalInferenceUrl = running && !String.IsNullOrWhiteSpace(state.LocalInferenceServerUrl)
                    ? state.LocalInferenceServerUrl
                    : "http://" + settings.LocalInferenceServer.Host + ":" + settings.LocalInferenceServer.Port,
                LocalInferenceLastError = state.LocalInferenceServerLastError ?? String.Empty,
                LocalInferenceLastRequestUtc = state.LocalInferenceServerLastRequestUtc,
                ProviderCount = settings.Runners.Count,
                ModelCount = settings.Runners.Sum(item => item.Models.Count),
                InventoryUpdatePending = ClientConnectionState.IsInventoryUpdatePending(),
                SettingsFile = ClientSettings.SettingsFile,
                LogFile = state.LogFilePath ?? ClientConnectionState.LogFile,
                ErrorLogFile = state.ErrorLogFilePath ?? ClientConnectionState.ErrorLogFile,
                CapturedUtc = DateTime.UtcNow
            };

            if (!running && !hasCredential)
            {
                snapshot.Status = ClientStatusEnum.NotSignedIn;
                snapshot.StatusText = "Not signed in";
                snapshot.StatusDetail = "Sign in to a Xeno server to share this machine's models and use the network's models.";
            }
            else if (!running)
            {
                snapshot.Status = ClientStatusEnum.Stopped;
                snapshot.StatusText = "Stopped";
                snapshot.StatusDetail = "The client is not running. Start it to connect this machine to the Xeno network.";
            }
            else if (snapshot.Connected && snapshot.Registered)
            {
                snapshot.Status = ClientStatusEnum.Connected;
                snapshot.StatusText = "Connected";
                snapshot.StatusDetail = "Connected to " + DisplayFormat.OrDash(snapshot.RestBaseUrl) + " and advertising " + snapshot.ModelCount + " model" + (snapshot.ModelCount == 1 ? String.Empty : "s") + ".";
            }
            else if (state.RegisteredUtc != null && state.DisconnectedUtc != null && state.DisconnectedUtc >= state.RegisteredUtc)
            {
                snapshot.Status = ClientStatusEnum.Reconnecting;
                snapshot.StatusText = "Reconnecting";
                snapshot.StatusDetail = String.IsNullOrWhiteSpace(snapshot.LastDisconnectReason)
                    ? "The connection to the Xeno server dropped. The client is reconnecting."
                    : "The connection dropped (" + snapshot.LastDisconnectReason + "). The client is reconnecting.";
            }
            else
            {
                snapshot.Status = ClientStatusEnum.Starting;
                snapshot.StatusText = "Starting";
                snapshot.StatusDetail = "The client is running and registering with the Xeno server.";
            }

            return snapshot;
        }

        #endregion
    }
}

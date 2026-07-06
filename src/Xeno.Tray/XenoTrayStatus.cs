namespace Xeno.Tray
{
    using Xeno.Client;

    /// <summary>
    /// Tray-facing Xeno client status snapshot.
    /// </summary>
    public sealed class XenoTrayStatus
    {
        /// <summary>
        /// Client display name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Client identifier.
        /// </summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>
        /// WebSocket URL.
        /// </summary>
        public string WebSocketUrl { get; set; } = string.Empty;

        /// <summary>
        /// Whether the recorded process is running.
        /// </summary>
        public bool Running { get; set; } = false;

        /// <summary>
        /// Whether the client is connected.
        /// </summary>
        public bool Connected { get; set; } = false;

        /// <summary>
        /// Whether the client is registered.
        /// </summary>
        public bool Registered { get; set; } = false;

        /// <summary>
        /// Process identifier.
        /// </summary>
        public int? ProcessId { get; set; } = null;

        /// <summary>
        /// Local dashboard URL.
        /// </summary>
        public string LocalDashboardUrl { get; set; } = string.Empty;

        /// <summary>
        /// Whether the local dashboard is listening.
        /// </summary>
        public bool LocalDashboardListening { get; set; } = false;

        /// <summary>
        /// Last disconnect reason.
        /// </summary>
        public string LastReason { get; set; } = string.Empty;

        /// <summary>
        /// Build status from client settings and connection state.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="state">Connection state.</param>
        /// <returns>Tray status.</returns>
        public static XenoTrayStatus From(ClientSettings settings, ClientConnectionState state)
        {
            return new XenoTrayStatus
            {
                Name = settings.Name ?? string.Empty,
                ClientId = settings.ClientId ?? state.ClientId ?? string.Empty,
                WebSocketUrl = settings.Server.Url ?? state.WebSocketUrl ?? string.Empty,
                Running = state.IsRecordedProcessRunning(),
                Connected = state.IsConnected,
                Registered = state.IsRegistered,
                ProcessId = state.ProcessId,
                LocalDashboardUrl = state.LocalDashboardUrl ?? string.Empty,
                LocalDashboardListening = state.LocalDashboardListening,
                LastReason = state.LastDisconnectReason ?? string.Empty
            };
        }
    }
}

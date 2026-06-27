namespace Xeno.Client.LocalDashboard.Models
{
    /// <summary>
    /// Local dashboard state response.
    /// </summary>
    public class LocalDashboardStateResponse
    {
        /// <summary>
        /// Client settings.
        /// </summary>
        public ClientSettings Settings { get; set; } = new ClientSettings();

        /// <summary>
        /// Client connection state.
        /// </summary>
        public ClientConnectionState Connection { get; set; } = new ClientConnectionState();

        /// <summary>
        /// Local dashboard URL.
        /// </summary>
        public string DashboardUrl { get; set; } = string.Empty;

        /// <summary>
        /// Local dashboard status.
        /// </summary>
        public LocalDashboardStatusEnum DashboardStatus { get; set; } = LocalDashboardStatusEnum.Disabled;
    }
}

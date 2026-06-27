namespace Xeno.Client.LocalDashboard.Models
{
    /// <summary>
    /// Local dashboard settings response.
    /// </summary>
    public class LocalDashboardSettingsResponse
    {
        /// <summary>
        /// Redacted client settings.
        /// </summary>
        public ClientSettings Settings { get; set; } = new ClientSettings();

        /// <summary>
        /// Client settings file path.
        /// </summary>
        public string SettingsFile { get; set; } = string.Empty;

        /// <summary>
        /// Whether the change requires restarting the Xeno client.
        /// </summary>
        public bool RestartRequired { get; set; } = false;
    }
}

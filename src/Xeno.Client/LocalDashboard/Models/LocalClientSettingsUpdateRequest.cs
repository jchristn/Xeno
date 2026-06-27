namespace Xeno.Client.LocalDashboard.Models
{
    /// <summary>
    /// Local client settings update request.
    /// </summary>
    public class LocalClientSettingsUpdateRequest : ClientLocalDashboardSettings
    {
        /// <summary>
        /// Telemetry settings.
        /// </summary>
        public ClientTelemetrySettings Telemetry { get; set; } = new ClientTelemetrySettings();
    }
}

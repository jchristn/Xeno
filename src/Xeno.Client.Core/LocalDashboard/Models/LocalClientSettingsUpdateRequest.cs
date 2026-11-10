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

        /// <summary>
        /// Local inference server settings.
        /// </summary>
        public ClientLocalInferenceServerSettings LocalInferenceServer { get; set; } = new ClientLocalInferenceServerSettings();

        /// <summary>
        /// Default maximum concurrent requests per provider. Null leaves the current value unchanged. Minimum 1, maximum 256.
        /// </summary>
        public int? DefaultMaxConcurrentRequests { get; set; } = null;
    }
}

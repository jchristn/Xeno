namespace Xeno.Core.Models.Telemetry
{
    /// <summary>
    /// Server-collected provider metric telemetry row.
    /// </summary>
    public class ServerProviderTelemetryMetricSampleRecord : ClientProviderTelemetryMetricSampleRecord
    {
        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId { get; set; } = string.Empty;
    }
}

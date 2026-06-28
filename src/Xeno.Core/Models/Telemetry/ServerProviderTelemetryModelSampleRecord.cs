namespace Xeno.Core.Models.Telemetry
{
    /// <summary>
    /// Server-collected provider model telemetry row.
    /// </summary>
    public class ServerProviderTelemetryModelSampleRecord : ClientProviderTelemetryModelSampleRecord
    {
        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId { get; set; } = string.Empty;
    }
}

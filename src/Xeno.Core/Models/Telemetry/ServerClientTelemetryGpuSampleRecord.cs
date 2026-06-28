namespace Xeno.Core.Models.Telemetry
{
    /// <summary>
    /// Server-collected GPU telemetry row.
    /// </summary>
    public class ServerClientTelemetryGpuSampleRecord : ClientTelemetryGpuSampleRecord
    {
        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId { get; set; } = string.Empty;
    }
}

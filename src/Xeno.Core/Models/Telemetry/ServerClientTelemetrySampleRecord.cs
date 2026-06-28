namespace Xeno.Core.Models.Telemetry
{
    /// <summary>
    /// Server-collected host telemetry sample summary.
    /// </summary>
    public class ServerClientTelemetrySampleRecord : ClientTelemetrySampleRecord
    {
        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// Client display name.
        /// </summary>
        public string? ClientName { get; set; } = null;
    }
}

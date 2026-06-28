namespace Xeno.Core.Models.Telemetry
{
    /// <summary>
    /// Server-collected provider telemetry sample summary.
    /// </summary>
    public class ServerProviderTelemetrySampleRecord : ClientProviderTelemetrySampleRecord
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

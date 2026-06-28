namespace Xeno.Core.Models.Telemetry
{
    /// <summary>
    /// Server host telemetry rollup request.
    /// </summary>
    public class ServerClientTelemetryRollupRequest : ClientTelemetryRollupRequest
    {
        /// <summary>
        /// Tenant filter for authorized callers.
        /// </summary>
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// Client identifier filter.
        /// </summary>
        public string? ClientId { get; set; } = null;
    }
}

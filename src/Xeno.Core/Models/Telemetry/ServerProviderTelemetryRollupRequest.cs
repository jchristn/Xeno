namespace Xeno.Core.Models.Telemetry
{
    /// <summary>
    /// Server provider telemetry rollup request.
    /// </summary>
    public class ServerProviderTelemetryRollupRequest : ClientProviderTelemetryRollupRequest
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

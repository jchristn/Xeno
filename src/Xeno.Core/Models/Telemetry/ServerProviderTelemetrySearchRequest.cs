namespace Xeno.Core.Models.Telemetry
{
    /// <summary>
    /// Server provider telemetry search request.
    /// </summary>
    public class ServerProviderTelemetrySearchRequest : ClientProviderTelemetrySearchRequest
    {
        /// <summary>
        /// Tenant filter for authorized callers.
        /// </summary>
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// Client identifier filter.
        /// </summary>
        public string? ClientId { get; set; } = null;

        /// <summary>
        /// Client display name filter.
        /// </summary>
        public string? ClientName { get; set; } = null;
    }
}

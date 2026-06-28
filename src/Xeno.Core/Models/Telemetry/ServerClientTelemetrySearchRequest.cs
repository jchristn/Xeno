namespace Xeno.Core.Models.Telemetry
{
    /// <summary>
    /// Server host telemetry search request.
    /// </summary>
    public class ServerClientTelemetrySearchRequest : ClientTelemetrySearchRequest
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

        /// <summary>
        /// Machine name filter.
        /// </summary>
        public string? MachineName { get; set; } = null;

        /// <summary>
        /// Maximum telemetry age in milliseconds for freshness filtering.
        /// </summary>
        public double? MaxAgeMs { get; set; } = null;
    }
}

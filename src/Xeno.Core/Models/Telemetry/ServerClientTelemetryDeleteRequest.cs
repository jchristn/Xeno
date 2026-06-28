namespace Xeno.Core.Models.Telemetry
{
    using System;

    /// <summary>
    /// Server telemetry bulk delete request.
    /// </summary>
    public class ServerClientTelemetryDeleteRequest
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
        /// Delete samples at or after this timestamp.
        /// </summary>
        public DateTime? FromUtc { get; set; } = null;

        /// <summary>
        /// Delete samples before this timestamp.
        /// </summary>
        public DateTime? ToUtc { get; set; } = null;
    }
}

namespace Xeno.Core.Models.Telemetry
{
    using System;
    using Xeno.Core.Helpers;

    /// <summary>
    /// Server telemetry collection attempt record.
    /// </summary>
    public class ServerClientTelemetryCollectionAttemptRecord
    {
        /// <summary>
        /// Collection attempt identifier.
        /// </summary>
        public string Id { get; set; } = IdGenerator.NewTelemetryCollectionRequestId();

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// Client identifier.
        /// </summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>
        /// Client display name.
        /// </summary>
        public string? ClientName { get; set; } = null;

        /// <summary>
        /// UTC timestamp when the attempt started.
        /// </summary>
        public DateTime StartedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// UTC timestamp when the attempt completed.
        /// </summary>
        public DateTime? CompletedUtc { get; set; } = null;

        /// <summary>
        /// Attempt elapsed time in milliseconds.
        /// </summary>
        public double? ElapsedMs { get; set; } = null;

        /// <summary>
        /// Whether host telemetry was requested.
        /// </summary>
        public bool HostRequested { get; set; } = true;

        /// <summary>
        /// Whether provider telemetry was requested.
        /// </summary>
        public bool ProviderRequested { get; set; } = true;

        /// <summary>
        /// Whether host telemetry succeeded.
        /// </summary>
        public bool HostSucceeded { get; set; } = false;

        /// <summary>
        /// Whether provider telemetry succeeded.
        /// </summary>
        public bool ProviderSucceeded { get; set; } = false;

        /// <summary>
        /// Attempt status.
        /// </summary>
        public string Status { get; set; } = "Started";

        /// <summary>
        /// Machine-readable error code.
        /// </summary>
        public string? ErrorCode { get; set; } = null;

        /// <summary>
        /// Safe error message.
        /// </summary>
        public string? ErrorMessage { get; set; } = null;

        /// <summary>
        /// Host telemetry sample identifier.
        /// </summary>
        public string? HostSampleId { get; set; } = null;

        /// <summary>
        /// Number of persisted provider telemetry samples.
        /// </summary>
        public int ProviderSampleCount { get; set; } = 0;
    }
}

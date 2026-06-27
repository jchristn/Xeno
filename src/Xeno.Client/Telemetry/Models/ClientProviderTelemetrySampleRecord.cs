namespace Xeno.Client.Telemetry.Models
{
    using System;

    /// <summary>
    /// Provider telemetry sample record.
    /// </summary>
    public class ClientProviderTelemetrySampleRecord
    {
        /// <summary>
        /// Sample ID.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Client ID.
        /// </summary>
        public string? ClientId { get; set; } = null;

        /// <summary>
        /// Provider name.
        /// </summary>
        public string ProviderName { get; set; } = string.Empty;

        /// <summary>
        /// Provider API type.
        /// </summary>
        public string ApiType { get; set; } = string.Empty;

        /// <summary>
        /// Provider telemetry kind.
        /// </summary>
        public ClientProviderTelemetryKindEnum ProviderKind { get; set; } = ClientProviderTelemetryKindEnum.Unknown;

        /// <summary>
        /// Endpoint hash.
        /// </summary>
        public string EndpointHash { get; set; } = string.Empty;

        /// <summary>
        /// Endpoint display value.
        /// </summary>
        public string EndpointDisplay { get; set; } = string.Empty;

        /// <summary>
        /// Collection timestamp.
        /// </summary>
        public DateTime CollectedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Persisted timestamp.
        /// </summary>
        public DateTime PersistedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Collection status.
        /// </summary>
        public ClientProviderTelemetryStatusEnum Status { get; set; } = ClientProviderTelemetryStatusEnum.Unavailable;

        /// <summary>
        /// Health status.
        /// </summary>
        public ClientProviderTelemetryHealthStatusEnum HealthStatus { get; set; } = ClientProviderTelemetryHealthStatusEnum.Unknown;

        /// <summary>
        /// Version.
        /// </summary>
        public string? Version { get; set; } = null;

        /// <summary>
        /// Available model count.
        /// </summary>
        public int AvailableModelCount { get; set; } = 0;

        /// <summary>
        /// Loaded model count.
        /// </summary>
        public int LoadedModelCount { get; set; } = 0;

        /// <summary>
        /// Active request count.
        /// </summary>
        public double? ActiveRequests { get; set; } = null;

        /// <summary>
        /// Queued request count.
        /// </summary>
        public double? QueuedRequests { get; set; } = null;

        /// <summary>
        /// Waiting request count.
        /// </summary>
        public double? WaitingRequests { get; set; } = null;

        /// <summary>
        /// Prompt token rate.
        /// </summary>
        public double? PromptTokensPerSecond { get; set; } = null;

        /// <summary>
        /// Generation token rate.
        /// </summary>
        public double? GenerationTokensPerSecond { get; set; } = null;

        /// <summary>
        /// Total token rate.
        /// </summary>
        public double? TotalTokensPerSecond { get; set; } = null;

        /// <summary>
        /// Request rate.
        /// </summary>
        public double? RequestRatePerSecond { get; set; } = null;

        /// <summary>
        /// Error rate.
        /// </summary>
        public double? ErrorRatePerSecond { get; set; } = null;

        /// <summary>
        /// Latency p95 in milliseconds.
        /// </summary>
        public double? LatencyP95Ms { get; set; } = null;

        /// <summary>
        /// Time to first token p95 in milliseconds.
        /// </summary>
        public double? TimeToFirstTokenP95Ms { get; set; } = null;

        /// <summary>
        /// Cache usage percentage.
        /// </summary>
        public double? CacheUsagePercent { get; set; } = null;

        /// <summary>
        /// Correlated process ID.
        /// </summary>
        public int? ProcessId { get; set; } = null;

        /// <summary>
        /// Correlated process name.
        /// </summary>
        public string? ProcessName { get; set; } = null;

        /// <summary>
        /// Last error.
        /// </summary>
        public string? LastError { get; set; } = null;
    }
}

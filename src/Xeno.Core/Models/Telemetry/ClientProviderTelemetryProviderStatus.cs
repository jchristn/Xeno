namespace Xeno.Core.Models.Telemetry
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Provider telemetry status.
    /// </summary>
    public class ClientProviderTelemetryProviderStatus
    {
        /// <summary>
        /// Endpoint metadata.
        /// </summary>
        public ClientProviderTelemetryEndpoint Endpoint { get; set; } = new ClientProviderTelemetryEndpoint();

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
        /// Swapped request count.
        /// </summary>
        public double? SwappedRequests { get; set; } = null;

        /// <summary>
        /// Prompt tokens per second.
        /// </summary>
        public double? PromptTokensPerSecond { get; set; } = null;

        /// <summary>
        /// Generation tokens per second.
        /// </summary>
        public double? GenerationTokensPerSecond { get; set; } = null;

        /// <summary>
        /// Total tokens per second.
        /// </summary>
        public double? TotalTokensPerSecond { get; set; } = null;

        /// <summary>
        /// Request rate per second.
        /// </summary>
        public double? RequestRatePerSecond { get; set; } = null;

        /// <summary>
        /// Error rate per second.
        /// </summary>
        public double? ErrorRatePerSecond { get; set; } = null;

        /// <summary>
        /// Latency p50 in milliseconds.
        /// </summary>
        public double? LatencyP50Ms { get; set; } = null;

        /// <summary>
        /// Latency p95 in milliseconds.
        /// </summary>
        public double? LatencyP95Ms { get; set; } = null;

        /// <summary>
        /// Latency p99 in milliseconds.
        /// </summary>
        public double? LatencyP99Ms { get; set; } = null;

        /// <summary>
        /// Time to first token p50 in milliseconds.
        /// </summary>
        public double? TimeToFirstTokenP50Ms { get; set; } = null;

        /// <summary>
        /// Time to first token p95 in milliseconds.
        /// </summary>
        public double? TimeToFirstTokenP95Ms { get; set; } = null;

        /// <summary>
        /// Cache usage percentage.
        /// </summary>
        public double? CacheUsagePercent { get; set; } = null;

        /// <summary>
        /// GPU cache usage percentage.
        /// </summary>
        public double? GpuCacheUsagePercent { get; set; } = null;

        /// <summary>
        /// CPU cache usage percentage.
        /// </summary>
        public double? CpuCacheUsagePercent { get; set; } = null;

        /// <summary>
        /// Memory used bytes.
        /// </summary>
        public long? MemoryUsedBytes { get; set; } = null;

        /// <summary>
        /// Memory total bytes.
        /// </summary>
        public long? MemoryTotalBytes { get; set; } = null;

        /// <summary>
        /// Correlated process ID when available.
        /// </summary>
        public int? ProcessId { get; set; } = null;

        /// <summary>
        /// Correlated process name when available.
        /// </summary>
        public string? ProcessName { get; set; } = null;

        /// <summary>
        /// Raw metrics were truncated.
        /// </summary>
        public bool RawMetricsTruncated { get; set; } = false;

        /// <summary>
        /// Raw metrics bytes.
        /// </summary>
        public int RawMetricsBytes { get; set; } = 0;

        /// <summary>
        /// Raw metrics text when enabled.
        /// </summary>
        public string? RawMetrics { get; set; } = null;

        /// <summary>
        /// Last error.
        /// </summary>
        public string? LastError { get; set; } = null;

        /// <summary>
        /// Collection timestamp.
        /// </summary>
        public DateTime CollectedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Provider models.
        /// </summary>
        public List<ClientProviderTelemetryModel> Models { get; set; } = new List<ClientProviderTelemetryModel>();

        /// <summary>
        /// Provider metrics.
        /// </summary>
        public List<ClientProviderTelemetryMetric> Metrics { get; set; } = new List<ClientProviderTelemetryMetric>();
    }
}

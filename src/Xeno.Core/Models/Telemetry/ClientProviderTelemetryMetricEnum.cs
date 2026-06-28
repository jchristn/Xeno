namespace Xeno.Core.Models.Telemetry
{
    /// <summary>
    /// Provider telemetry metric.
    /// </summary>
    public enum ClientProviderTelemetryMetricEnum
    {
        /// <summary>
        /// Active request count.
        /// </summary>
        ActiveRequests,

        /// <summary>
        /// Queued request count.
        /// </summary>
        QueuedRequests,

        /// <summary>
        /// Waiting request count.
        /// </summary>
        WaitingRequests,

        /// <summary>
        /// Request rate per second.
        /// </summary>
        RequestRatePerSecond,

        /// <summary>
        /// Error rate per second.
        /// </summary>
        ErrorRatePerSecond,

        /// <summary>
        /// Prompt token rate per second.
        /// </summary>
        PromptTokensPerSecond,

        /// <summary>
        /// Generation token rate per second.
        /// </summary>
        GenerationTokensPerSecond,

        /// <summary>
        /// Total token rate per second.
        /// </summary>
        TotalTokensPerSecond,

        /// <summary>
        /// Latency p95 in milliseconds.
        /// </summary>
        LatencyP95Ms,

        /// <summary>
        /// Time to first token p95 in milliseconds.
        /// </summary>
        TimeToFirstTokenP95Ms,

        /// <summary>
        /// Cache usage percentage.
        /// </summary>
        CacheUsagePercent,

        /// <summary>
        /// Loaded model count.
        /// </summary>
        LoadedModelCount,

        /// <summary>
        /// Available model count.
        /// </summary>
        AvailableModelCount
    }
}

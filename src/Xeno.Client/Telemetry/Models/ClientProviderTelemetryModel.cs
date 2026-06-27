namespace Xeno.Client.Telemetry.Models
{
    using System;

    /// <summary>
    /// Provider model telemetry.
    /// </summary>
    public class ClientProviderTelemetryModel
    {
        /// <summary>
        /// Model name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Model family.
        /// </summary>
        public string? Family { get; set; } = null;

        /// <summary>
        /// Quantization.
        /// </summary>
        public string? Quantization { get; set; } = null;

        /// <summary>
        /// Parameter size.
        /// </summary>
        public string? ParameterSize { get; set; } = null;

        /// <summary>
        /// Context length.
        /// </summary>
        public int? ContextLength { get; set; } = null;

        /// <summary>
        /// Whether the model is loaded.
        /// </summary>
        public bool Loaded { get; set; } = false;

        /// <summary>
        /// Size in bytes.
        /// </summary>
        public long? SizeBytes { get; set; } = null;

        /// <summary>
        /// Memory in bytes.
        /// </summary>
        public long? MemoryBytes { get; set; } = null;

        /// <summary>
        /// Model expiration timestamp.
        /// </summary>
        public DateTime? ExpiresUtc { get; set; } = null;

        /// <summary>
        /// Active request count.
        /// </summary>
        public double? ActiveRequests { get; set; } = null;

        /// <summary>
        /// Queued request count.
        /// </summary>
        public double? QueuedRequests { get; set; } = null;

        /// <summary>
        /// Prompt token rate.
        /// </summary>
        public double? PromptTokensPerSecond { get; set; } = null;

        /// <summary>
        /// Generation token rate.
        /// </summary>
        public double? GenerationTokensPerSecond { get; set; } = null;

        /// <summary>
        /// Latency p95 in milliseconds.
        /// </summary>
        public double? LatencyP95Ms { get; set; } = null;

        /// <summary>
        /// Time to first token p95 in milliseconds.
        /// </summary>
        public double? TimeToFirstTokenP95Ms { get; set; } = null;
    }
}

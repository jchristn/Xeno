namespace Xeno.Core.Models.Telemetry
{
    using System;

    /// <summary>
    /// Provider model telemetry sample record.
    /// </summary>
    public class ClientProviderTelemetryModelSampleRecord
    {
        /// <summary>
        /// Row ID.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Sample ID.
        /// </summary>
        public string SampleId { get; set; } = string.Empty;

        /// <summary>
        /// Provider name.
        /// </summary>
        public string ProviderName { get; set; } = string.Empty;

        /// <summary>
        /// Model name.
        /// </summary>
        public string ModelName { get; set; } = string.Empty;

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
        /// Whether loaded.
        /// </summary>
        public bool Loaded { get; set; } = false;

        /// <summary>
        /// Size bytes.
        /// </summary>
        public long? SizeBytes { get; set; } = null;

        /// <summary>
        /// Memory bytes.
        /// </summary>
        public long? MemoryBytes { get; set; } = null;

        /// <summary>
        /// Expires timestamp.
        /// </summary>
        public DateTime? ExpiresUtc { get; set; } = null;
    }
}

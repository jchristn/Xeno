namespace Xeno.Core.Models
{
    using System;

    /// <summary>
    /// Single model performance request result.
    /// </summary>
    public class ModelPerformanceTestSampleResult
    {
        /// <summary>
        /// One-based sample index.
        /// </summary>
        public int Index { get; set; } = 0;

        /// <summary>
        /// Whether the request succeeded.
        /// </summary>
        public bool Success { get; set; } = false;

        /// <summary>
        /// HTTP status code returned by the provider or relay path.
        /// </summary>
        public int? StatusCode { get; set; } = null;

        /// <summary>
        /// Total elapsed time in milliseconds.
        /// </summary>
        public double? TotalTimeMs { get; set; } = null;

        /// <summary>
        /// Time to first token or first response byte in milliseconds.
        /// </summary>
        public double? TimeToFirstTokenMs { get; set; } = null;

        /// <summary>
        /// Time to last token or final response byte in milliseconds.
        /// </summary>
        public double? TimeToLastTokenMs { get; set; } = null;

        /// <summary>
        /// Output tokens per second when token counts are available.
        /// </summary>
        public double? TokensPerSecond { get; set; } = null;

        /// <summary>
        /// Prompt token count when reported by the provider.
        /// </summary>
        public int? PromptTokens { get; set; } = null;

        /// <summary>
        /// Completion token count when reported by the provider.
        /// </summary>
        public int? CompletionTokens { get; set; } = null;

        /// <summary>
        /// Total token count when reported by the provider.
        /// </summary>
        public int? TotalTokens { get; set; } = null;

        /// <summary>
        /// Number of streaming chunks observed.
        /// </summary>
        public int? StreamChunkCount { get; set; } = null;

        /// <summary>
        /// Request body bytes.
        /// </summary>
        public long? RequestBytes { get; set; } = null;

        /// <summary>
        /// Response body bytes.
        /// </summary>
        public long? ResponseBytes { get; set; } = null;

        /// <summary>
        /// Error message when the sample failed.
        /// </summary>
        public string? Error { get; set; } = null;

        /// <summary>
        /// UTC timestamp when the sample started.
        /// </summary>
        public DateTime StartedUtc { get; set; } = DateTime.UtcNow;
    }
}

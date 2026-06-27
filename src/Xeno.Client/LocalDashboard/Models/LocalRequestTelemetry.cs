namespace Xeno.Client.LocalDashboard.Models
{
    /// <summary>
    /// Local request telemetry.
    /// </summary>
    public class LocalRequestTelemetry
    {
        /// <summary>
        /// Total duration in milliseconds.
        /// </summary>
        public double? DurationMs { get; set; } = null;

        /// <summary>
        /// Time to response headers in milliseconds.
        /// </summary>
        public double? TimeToHeadersMs { get; set; } = null;

        /// <summary>
        /// Time to first byte or token in milliseconds.
        /// </summary>
        public double? TimeToFirstByteMs { get; set; } = null;

        /// <summary>
        /// Total tokens when available.
        /// </summary>
        public int? TotalTokens { get; set; } = null;

        /// <summary>
        /// Prompt tokens when available.
        /// </summary>
        public int? PromptTokens { get; set; } = null;

        /// <summary>
        /// Completion tokens when available.
        /// </summary>
        public int? CompletionTokens { get; set; } = null;

        /// <summary>
        /// Stream chunk count.
        /// </summary>
        public int StreamChunkCount { get; set; } = 0;

        /// <summary>
        /// Stream event count.
        /// </summary>
        public int StreamEventCount { get; set; } = 0;
    }
}

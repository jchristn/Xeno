namespace Xeno.Core.Models
{
    /// <summary>
    /// Aggregate model performance metrics for a benchmark leg.
    /// </summary>
    public class ModelPerformanceTestSummary
    {
        /// <summary>
        /// Number of successful samples.
        /// </summary>
        public int SuccessCount { get; set; } = 0;

        /// <summary>
        /// Number of failed samples.
        /// </summary>
        public int FailureCount { get; set; } = 0;

        /// <summary>
        /// Average total elapsed time in milliseconds.
        /// </summary>
        public double? AverageTotalTimeMs { get; set; } = null;

        /// <summary>
        /// Median total elapsed time in milliseconds.
        /// </summary>
        public double? MedianTotalTimeMs { get; set; } = null;

        /// <summary>
        /// Minimum total elapsed time in milliseconds.
        /// </summary>
        public double? MinimumTotalTimeMs { get; set; } = null;

        /// <summary>
        /// Maximum total elapsed time in milliseconds.
        /// </summary>
        public double? MaximumTotalTimeMs { get; set; } = null;

        /// <summary>
        /// Average time to first token in milliseconds.
        /// </summary>
        public double? AverageTimeToFirstTokenMs { get; set; } = null;

        /// <summary>
        /// Median time to first token in milliseconds.
        /// </summary>
        public double? MedianTimeToFirstTokenMs { get; set; } = null;

        /// <summary>
        /// Average time to last token in milliseconds.
        /// </summary>
        public double? AverageTimeToLastTokenMs { get; set; } = null;

        /// <summary>
        /// Average output tokens per second.
        /// </summary>
        public double? AverageTokensPerSecond { get; set; } = null;

        /// <summary>
        /// Average response bytes.
        /// </summary>
        public double? AverageResponseBytes { get; set; } = null;
    }
}

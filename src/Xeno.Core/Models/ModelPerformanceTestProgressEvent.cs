namespace Xeno.Core.Models
{
    using System;

    /// <summary>
    /// Dashboard-facing model performance test progress event.
    /// </summary>
    public class ModelPerformanceTestProgressEvent
    {
        /// <summary>
        /// Progress event type such as status, legStarted, warmupStarted, warmupCompleted, sampleCompleted, legCompleted, completed, or error.
        /// </summary>
        public string EventType { get; set; } = String.Empty;

        /// <summary>
        /// Parent dashboard request ID.
        /// </summary>
        public string RequestId { get; set; } = String.Empty;

        /// <summary>
        /// Leg name, native or hairpin, when the event applies to one leg.
        /// </summary>
        public string? Leg { get; set; } = null;

        /// <summary>
        /// Human-readable status message.
        /// </summary>
        public string Message { get; set; } = String.Empty;

        /// <summary>
        /// One-based measured sample index when the event applies to a measured sample. Warmup completion may use 0.
        /// </summary>
        public int? Index { get; set; } = null;

        /// <summary>
        /// Completed sample count for the leg.
        /// </summary>
        public int? CompletedSamples { get; set; } = null;

        /// <summary>
        /// Total sample count expected for the leg.
        /// </summary>
        public int? TotalSamples { get; set; } = null;

        /// <summary>
        /// Sample result when a sample has completed.
        /// </summary>
        public ModelPerformanceTestSampleResult? Sample { get; set; } = null;

        /// <summary>
        /// Current leg summary when available.
        /// </summary>
        public ModelPerformanceTestSummary? Summary { get; set; } = null;

        /// <summary>
        /// Complete leg result when the leg has completed.
        /// </summary>
        public ModelPerformanceTestLegResult? LegResult { get; set; } = null;

        /// <summary>
        /// Complete model performance result when the benchmark has completed.
        /// </summary>
        public ModelPerformanceTestResponse? Result { get; set; } = null;

        /// <summary>
        /// UTC timestamp when the event was emitted.
        /// </summary>
        public DateTime SentUtc { get; set; } = DateTime.UtcNow;
    }
}

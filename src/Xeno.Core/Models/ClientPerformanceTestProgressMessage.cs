namespace Xeno.Core.Models
{
    using System;

    /// <summary>
    /// Client-to-server progress message for a running model performance test leg.
    /// </summary>
    public class ClientPerformanceTestProgressMessage
    {
        /// <summary>
        /// Message type.
        /// </summary>
        public string Type { get; set; } = "client.performanceTest.progress";

        /// <summary>
        /// Benchmark leg request ID.
        /// </summary>
        public string RequestId { get; set; } = String.Empty;

        /// <summary>
        /// Client ID sending the progress event.
        /// </summary>
        public string ClientId { get; set; } = String.Empty;

        /// <summary>
        /// Tenant ID.
        /// </summary>
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// Leg mode, direct or relay.
        /// </summary>
        public string Mode { get; set; } = String.Empty;

        /// <summary>
        /// Progress event type such as legStarted, warmupStarted, warmupCompleted, sampleCompleted, or legCompleted.
        /// </summary>
        public string EventType { get; set; } = String.Empty;

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
        /// UTC timestamp when the client emitted this message.
        /// </summary>
        public DateTime SentUtc { get; set; } = DateTime.UtcNow;
    }
}

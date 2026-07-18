namespace Xeno.Core.Models
{
    using System;

    /// <summary>
    /// Server-to-source-client response for one relayed benchmark sample.
    /// </summary>
    public class ServerPerformanceTestRelayResponseMessage : WebSocketMessageEnvelope
    {
        /// <summary>
        /// Parent performance test request ID.
        /// </summary>
        public string RequestId { get; set; } = String.Empty;

        /// <summary>
        /// Per-sample request ID.
        /// </summary>
        public string SampleId { get; set; } = String.Empty;

        /// <summary>
        /// One-based measured sample index. Warmup relay responses use 0.
        /// </summary>
        public int Index { get; set; } = 0;

        /// <summary>
        /// Whether the sample succeeded.
        /// </summary>
        public bool Success { get; set; } = false;

        /// <summary>
        /// Sample result.
        /// </summary>
        public ModelPerformanceTestSampleResult Result { get; set; } = new ModelPerformanceTestSampleResult();

        /// <summary>
        /// Error message when the relay failed.
        /// </summary>
        public string? Error { get; set; } = null;
    }
}

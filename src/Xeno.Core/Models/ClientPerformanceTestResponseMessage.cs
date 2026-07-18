namespace Xeno.Core.Models
{
    using System;

    /// <summary>
    /// Client-to-server response containing a completed performance benchmark leg.
    /// </summary>
    public class ClientPerformanceTestResponseMessage : WebSocketMessageEnvelope
    {
        /// <summary>
        /// Performance test request ID.
        /// </summary>
        public string RequestId { get; set; } = String.Empty;

        /// <summary>
        /// Client ID sending the response.
        /// </summary>
        public string ClientId { get; set; } = String.Empty;

        /// <summary>
        /// Tenant ID.
        /// </summary>
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// Benchmark mode: direct or relay.
        /// </summary>
        public string Mode { get; set; } = "direct";

        /// <summary>
        /// Whether the benchmark leg completed.
        /// </summary>
        public bool Success { get; set; } = false;

        /// <summary>
        /// Result leg.
        /// </summary>
        public ModelPerformanceTestLegResult Result { get; set; } = new ModelPerformanceTestLegResult();

        /// <summary>
        /// Error message when the leg failed.
        /// </summary>
        public string? Error { get; set; } = null;
    }
}

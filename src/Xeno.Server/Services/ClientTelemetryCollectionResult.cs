namespace Xeno.Server.Services
{
    using System.Collections.Generic;

    /// <summary>
    /// Internal telemetry collection cycle result.
    /// </summary>
    public class ClientTelemetryCollectionResult
    {
        /// <summary>
        /// Attempt identifiers created during the cycle.
        /// </summary>
        public List<string> AttemptIds { get; set; } = new List<string>();

        /// <summary>
        /// Number of target clients selected for the cycle.
        /// </summary>
        public int TargetClientCount { get; set; } = 0;

        /// <summary>
        /// Number of targets skipped because another collection cycle was active.
        /// </summary>
        public int SkippedOverlapCount { get; set; } = 0;
    }
}

namespace Xeno.Core.Models.Telemetry
{
    using System.Collections.Generic;

    /// <summary>
    /// Manual telemetry collection trigger response.
    /// </summary>
    public class ServerClientTelemetryCollectResponse
    {
        /// <summary>
        /// Collection attempt identifiers.
        /// </summary>
        public List<string> AttemptIds { get; set; } = new List<string>();

        /// <summary>
        /// Number of clients targeted.
        /// </summary>
        public int TargetClientCount { get; set; } = 0;
    }
}

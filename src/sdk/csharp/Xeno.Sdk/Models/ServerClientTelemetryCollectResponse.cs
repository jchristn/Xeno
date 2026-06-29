namespace Xeno.Sdk
{
    using System.Collections.Generic;

    /// <summary>
    /// Xeno server client telemetry collect response DTO.
    /// </summary>
    public class ServerClientTelemetryCollectResponse
    {
        /// <summary>
        /// Gets or sets the attempt ids.
        /// </summary>
        public List<string> AttemptIds { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the target client count.
        /// </summary>
        public int TargetClientCount { get; set; }
    }
}

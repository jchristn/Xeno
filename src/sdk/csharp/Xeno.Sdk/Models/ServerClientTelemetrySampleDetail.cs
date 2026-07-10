namespace Xeno.Sdk
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Xeno server client telemetry sample detail DTO.
    /// </summary>
    public class ServerClientTelemetrySampleDetail
    {
        /// <summary>
        /// Gets or sets the sample.
        /// </summary>
        public ServerClientTelemetrySampleRecord? Sample { get; set; }

        /// <summary>
        /// Gets or sets the gpu samples.
        /// </summary>
        public List<RawJsonValue> GpuSamples { get; set; } = new List<RawJsonValue>();

        /// <summary>
        /// Gets or sets the raw snapshot json.
        /// </summary>
        public string RawSnapshotJson { get; set; } = String.Empty;
    }
}

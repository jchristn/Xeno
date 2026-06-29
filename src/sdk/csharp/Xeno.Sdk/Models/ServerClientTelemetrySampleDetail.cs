namespace Xeno.Sdk
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

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
        public List<JsonElement> GpuSamples { get; set; } = new List<JsonElement>();

        /// <summary>
        /// Gets or sets the raw snapshot json.
        /// </summary>
        public string RawSnapshotJson { get; set; } = String.Empty;
    }
}

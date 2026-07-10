namespace Xeno.Sdk
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Xeno server provider telemetry sample detail DTO.
    /// </summary>
    public class ServerProviderTelemetrySampleDetail
    {
        /// <summary>
        /// Gets or sets the sample.
        /// </summary>
        public ServerProviderTelemetrySampleRecord? Sample { get; set; }

        /// <summary>
        /// Gets or sets the models.
        /// </summary>
        public List<RawJsonValue> Models { get; set; } = new List<RawJsonValue>();

        /// <summary>
        /// Gets or sets the metrics.
        /// </summary>
        public List<RawJsonValue> Metrics { get; set; } = new List<RawJsonValue>();

        /// <summary>
        /// Gets or sets the raw snapshot json.
        /// </summary>
        public string RawSnapshotJson { get; set; } = String.Empty;
    }
}

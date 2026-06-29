namespace Xeno.Sdk
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

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
        public List<JsonElement> Models { get; set; } = new List<JsonElement>();

        /// <summary>
        /// Gets or sets the metrics.
        /// </summary>
        public List<JsonElement> Metrics { get; set; } = new List<JsonElement>();

        /// <summary>
        /// Gets or sets the raw snapshot json.
        /// </summary>
        public string RawSnapshotJson { get; set; } = String.Empty;
    }
}

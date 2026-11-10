namespace Xeno.Client.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// One model in an OpenAI-compatible model list.
    /// </summary>
    public class LocalOpenAiModel
    {
        /// <summary>
        /// Model identifier. Never null.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Object type, always "model".
        /// </summary>
        [JsonPropertyName("object")]
        public string Object { get; set; } = "model";

        /// <summary>
        /// Creation time as Unix seconds. Zero when unknown.
        /// </summary>
        [JsonPropertyName("created")]
        public long Created { get; set; } = 0;

        /// <summary>
        /// Owner. For models served on this machine, the provider name; for network models, the value the Xeno network reports.
        /// </summary>
        [JsonPropertyName("owned_by")]
        public string OwnedBy { get; set; } = String.Empty;
    }
}

namespace Xeno.Client.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// One model in an Ollama /api/tags response.
    /// </summary>
    public class LocalOllamaModel
    {
        /// <summary>
        /// Model name. Never null.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Model identifier, usually the same as the name. Never null.
        /// </summary>
        [JsonPropertyName("model")]
        public string Model { get; set; } = String.Empty;

        /// <summary>
        /// Last modified time as the provider reported it (RFC 3339), when known.
        /// </summary>
        [JsonPropertyName("modified_at")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ModifiedAt { get; set; } = null;

        /// <summary>
        /// Size in bytes. Zero for remote (cloud) models and network models.
        /// </summary>
        [JsonPropertyName("size")]
        public long Size { get; set; } = 0;

        /// <summary>
        /// Content digest, when known.
        /// </summary>
        [JsonPropertyName("digest")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Digest { get; set; } = null;

        /// <summary>
        /// Upstream model name for an Ollama Cloud model.
        /// </summary>
        [JsonPropertyName("remote_model")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? RemoteModel { get; set; } = null;

        /// <summary>
        /// Upstream host for an Ollama Cloud model.
        /// </summary>
        [JsonPropertyName("remote_host")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? RemoteHost { get; set; } = null;

        /// <summary>
        /// Model details, when known.
        /// </summary>
        [JsonPropertyName("details")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public LocalOllamaModelDetails? Details { get; set; } = null;
    }
}

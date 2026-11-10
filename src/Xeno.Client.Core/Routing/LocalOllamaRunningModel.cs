namespace Xeno.Client.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// One loaded model in an Ollama /api/ps response.
    /// </summary>
    public class LocalOllamaRunningModel
    {
        /// <summary>
        /// Model name. Never null.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Model identifier. Never null.
        /// </summary>
        [JsonPropertyName("model")]
        public string Model { get; set; } = String.Empty;

        /// <summary>
        /// Memory size in bytes.
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
        /// Model details, when known.
        /// </summary>
        [JsonPropertyName("details")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public LocalOllamaModelDetails? Details { get; set; } = null;

        /// <summary>
        /// When the model unloads, as the provider reported it (RFC 3339), when known.
        /// </summary>
        [JsonPropertyName("expires_at")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ExpiresAt { get; set; } = null;

        /// <summary>
        /// Bytes resident in GPU memory.
        /// </summary>
        [JsonPropertyName("size_vram")]
        public long SizeVram { get; set; } = 0;

        /// <summary>
        /// Loaded context length, when known.
        /// </summary>
        [JsonPropertyName("context_length")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? ContextLength { get; set; } = null;
    }
}

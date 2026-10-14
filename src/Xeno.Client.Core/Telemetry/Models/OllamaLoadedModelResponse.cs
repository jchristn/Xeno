namespace Xeno.Client.Telemetry.Models
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Ollama loaded model response.
    /// </summary>
    public class OllamaLoadedModelResponse
    {
        /// <summary>
        /// Model name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Size in bytes.
        /// </summary>
        public long Size { get; set; } = 0;

        /// <summary>
        /// Size in VRAM.
        /// </summary>
        [JsonPropertyName("size_vram")]
        public long SizeVram { get; set; } = 0;

        /// <summary>
        /// Expiration timestamp.
        /// </summary>
        [JsonPropertyName("expires_at")]
        public DateTime? ExpiresAt { get; set; } = null;

        /// <summary>
        /// Details.
        /// </summary>
        public OllamaModelDetailsResponse? Details { get; set; } = null;
    }
}

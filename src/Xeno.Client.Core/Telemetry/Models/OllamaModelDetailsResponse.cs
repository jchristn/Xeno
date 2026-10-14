namespace Xeno.Client.Telemetry.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Ollama model details response.
    /// </summary>
    public class OllamaModelDetailsResponse
    {
        /// <summary>
        /// Model format.
        /// </summary>
        public string? Format { get; set; } = null;

        /// <summary>
        /// Model family.
        /// </summary>
        public string? Family { get; set; } = null;

        /// <summary>
        /// Parameter size.
        /// </summary>
        [JsonPropertyName("parameter_size")]
        public string? ParameterSize { get; set; } = null;

        /// <summary>
        /// Quantization level.
        /// </summary>
        [JsonPropertyName("quantization_level")]
        public string? QuantizationLevel { get; set; } = null;
    }
}

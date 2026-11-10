namespace Xeno.Client.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Ollama model details object.
    /// </summary>
    public class LocalOllamaModelDetails
    {
        /// <summary>
        /// Parent model, when the model was created from another.
        /// </summary>
        [JsonPropertyName("parent_model")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ParentModel { get; set; } = null;

        /// <summary>
        /// Weights format, for example "gguf".
        /// </summary>
        [JsonPropertyName("format")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Format { get; set; } = null;

        /// <summary>
        /// Model family.
        /// </summary>
        [JsonPropertyName("family")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Family { get; set; } = null;

        /// <summary>
        /// Model families.
        /// </summary>
        [JsonPropertyName("families")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? Families { get; set; } = null;

        /// <summary>
        /// Parameter size, for example "8B".
        /// </summary>
        [JsonPropertyName("parameter_size")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ParameterSize { get; set; } = null;

        /// <summary>
        /// Quantization level, for example "Q4_K_M".
        /// </summary>
        [JsonPropertyName("quantization_level")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? QuantizationLevel { get; set; } = null;
    }
}

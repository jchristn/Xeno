namespace Xeno.Sdk
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Token usage totals for one model.
    /// </summary>
    public class ModelTokenUsage
    {
        /// <summary>
        /// Gets or sets number of request-history entries processed for the model.
        /// </summary>
        [JsonPropertyName("requestCount")]
        public long RequestCount { get; set; }

        /// <summary>
        /// Gets or sets total tokens reported for the model.
        /// </summary>
        [JsonPropertyName("totalTokens")]
        public long TotalTokens { get; set; }

        /// <summary>
        /// Gets or sets prompt/input tokens reported for the model.
        /// </summary>
        [JsonPropertyName("promptTokens")]
        public long PromptTokens { get; set; }

        /// <summary>
        /// Gets or sets output/completion tokens reported for the model.
        /// </summary>
        [JsonPropertyName("outputTokens")]
        public long OutputTokens { get; set; }
    }
}

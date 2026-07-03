namespace Xeno.Core.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// OpenAI-compatible response envelope with a usage block.
    /// </summary>
    public class OpenAiUsageEnvelope
    {
        /// <summary>
        /// Usage metrics.
        /// </summary>
        [JsonPropertyName("usage")]
        public OpenAiUsage? Usage { get; set; } = null;

        /// <summary>
        /// Ollama-native prompt evaluation token count when usage is reported at the response root.
        /// </summary>
        [JsonPropertyName("prompt_eval_count")]
        public int? PromptEvalCount { get; set; } = null;

        /// <summary>
        /// Ollama-native response evaluation token count when usage is reported at the response root.
        /// </summary>
        [JsonPropertyName("eval_count")]
        public int? EvalCount { get; set; } = null;

        /// <summary>
        /// Effective usage block.
        /// </summary>
        [JsonIgnore]
        public OpenAiUsage EffectiveUsage
        {
            get
            {
                if (Usage != null) return Usage;
                return new OpenAiUsage
                {
                    PromptEvalCount = PromptEvalCount,
                    EvalCount = EvalCount
                };
            }
        }
    }
}

namespace Xeno.Core.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// OpenAI-compatible usage block.
    /// </summary>
    public class OpenAiUsage
    {
        /// <summary>
        /// Prompt token count.
        /// </summary>
        [JsonPropertyName("prompt_tokens")]
        public int? PromptTokensSnake { get; set; } = null;

        /// <summary>
        /// Completion token count.
        /// </summary>
        [JsonPropertyName("completion_tokens")]
        public int? CompletionTokensSnake { get; set; } = null;

        /// <summary>
        /// Total token count.
        /// </summary>
        [JsonPropertyName("total_tokens")]
        public int? TotalTokensSnake { get; set; } = null;

        /// <summary>
        /// Prompt token count.
        /// </summary>
        [JsonPropertyName("promptTokens")]
        public int? PromptTokensCamel { get; set; } = null;

        /// <summary>
        /// Completion token count.
        /// </summary>
        [JsonPropertyName("completionTokens")]
        public int? CompletionTokensCamel { get; set; } = null;

        /// <summary>
        /// Total token count.
        /// </summary>
        [JsonPropertyName("totalTokens")]
        public int? TotalTokensCamel { get; set; } = null;

        /// <summary>
        /// Ollama-native prompt evaluation token count.
        /// </summary>
        [JsonPropertyName("prompt_eval_count")]
        public int? PromptEvalCount { get; set; } = null;

        /// <summary>
        /// Ollama-native response evaluation token count.
        /// </summary>
        [JsonPropertyName("eval_count")]
        public int? EvalCount { get; set; } = null;

        /// <summary>
        /// Effective prompt token count.
        /// </summary>
        [JsonIgnore]
        public int? PromptTokens
        {
            get { return PromptTokensSnake ?? PromptTokensCamel ?? PromptEvalCount; }
        }

        /// <summary>
        /// Effective completion token count.
        /// </summary>
        [JsonIgnore]
        public int? CompletionTokens
        {
            get { return CompletionTokensSnake ?? CompletionTokensCamel ?? EvalCount; }
        }

        /// <summary>
        /// Effective total token count.
        /// </summary>
        [JsonIgnore]
        public int? TotalTokens
        {
            get
            {
                if (TotalTokensSnake.HasValue) return TotalTokensSnake;
                if (TotalTokensCamel.HasValue) return TotalTokensCamel;
                if (!PromptTokens.HasValue && !CompletionTokens.HasValue) return null;
                return (PromptTokens ?? 0) + (CompletionTokens ?? 0);
            }
        }

        /// <summary>
        /// Whether any usage value was supplied.
        /// </summary>
        [JsonIgnore]
        public bool HasAny
        {
            get { return PromptTokens.HasValue || CompletionTokens.HasValue || TotalTokens.HasValue; }
        }
    }
}

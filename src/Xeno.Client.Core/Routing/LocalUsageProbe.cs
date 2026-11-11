namespace Xeno.Client.Routing
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Xeno.Core.Serialization;

    /// <summary>
    /// Reads token counts from a complete (non-streaming) OpenAI-compatible or Ollama-compatible response body.
    /// Only the usage fields are read into this named type; nothing else in the body is inspected or kept.
    /// </summary>
    public class LocalUsageProbe
    {
        /// <summary>
        /// OpenAI-compatible usage object, when present.
        /// </summary>
        [JsonPropertyName("usage")]
        public LocalUsageCounts? Usage { get; set; } = null;

        /// <summary>
        /// Ollama prompt token count, when present.
        /// </summary>
        [JsonPropertyName("prompt_eval_count")]
        public long? PromptEvalCount { get; set; } = null;

        /// <summary>
        /// Ollama completion token count, when present.
        /// </summary>
        [JsonPropertyName("eval_count")]
        public long? EvalCount { get; set; } = null;

        /// <summary>
        /// Prompt tokens from either shape, or null.
        /// </summary>
        [JsonIgnore]
        public long? PromptTokens
        {
            get
            {
                return Usage?.PromptTokens ?? PromptEvalCount;
            }
        }

        /// <summary>
        /// Completion tokens from either shape, or null.
        /// </summary>
        [JsonIgnore]
        public long? CompletionTokens
        {
            get
            {
                return Usage?.CompletionTokens ?? EvalCount;
            }
        }

        /// <summary>
        /// Read usage from a response body.
        /// </summary>
        /// <param name="body">Response body. Null or non-JSON bodies return null.</param>
        /// <returns>Probe, or null.</returns>
        public static LocalUsageProbe? Read(byte[]? body)
        {
            if (body == null || body.Length == 0 || body.Length > 16 * 1024 * 1024) return null;
            try
            {
                return JsonUtil.Deserialize<LocalUsageProbe>(System.Text.Encoding.UTF8.GetString(body));
            }
            catch (Exception ex) when (ex is JsonException || ex is System.IO.InvalidDataException)
            {
                return null;
            }
        }
    }
}

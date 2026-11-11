namespace Xeno.Core.Chat
{
    using System;
    using System.Linq;

    /// <summary>
    /// Recognizes embedding-only models by name, for providers that do not say what a model can do. Chat model lists
    /// leave them out.
    /// </summary>
    public static class ChatModelNames
    {
        private static readonly string[] _EmbeddingMarkers =
        {
            "embed", "minilm", "bge-", "bge:", "gte-", "gte:", "e5-", "e5:", "multilingual-e5", "paraphrase-", "sentence-t5", "rerank"
        };

        /// <summary>
        /// True when a model makes embeddings and cannot chat: it supports embeddings and not completions, or its name
        /// is a known embedding family (nomic-embed-text, mxbai-embed-large, all-minilm, bge, gte, e5, snowflake-arctic-embed).
        /// </summary>
        /// <param name="name">Model name.</param>
        /// <param name="supportsCompletions">Whether the provider says it completes text.</param>
        /// <param name="supportsEmbeddings">Whether the provider says it makes embeddings.</param>
        /// <returns>True for an embedding-only model.</returns>
        public static bool IsEmbeddingOnly(string? name, bool supportsCompletions, bool supportsEmbeddings)
        {
            if (supportsEmbeddings && !supportsCompletions) return true;
            if (String.IsNullOrWhiteSpace(name)) return false;
            string lower = name.Trim().ToLowerInvariant();
            int slash = lower.LastIndexOf('/');
            if (slash >= 0) lower = lower.Substring(slash + 1);
            return _EmbeddingMarkers.Any(marker => lower.StartsWith(marker, StringComparison.Ordinal) || lower.Contains("-" + marker, StringComparison.Ordinal) || (marker == "embed" && lower.Contains("embed", StringComparison.Ordinal)));
        }
    }
}

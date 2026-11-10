namespace Xeno.Client.Services
{
    using System;

    /// <summary>
    /// Normalizes model names the way Ollama does, so "llama3.1" and "llama3.1:latest" compare equal.
    /// Thread safe; the class is stateless.
    /// </summary>
    public static class LocalModelNameNormalizer
    {
        private const string LatestTag = ":latest";

        /// <summary>
        /// Normalize a model name: trim whitespace and add the ":latest" tag when the name has no tag.
        /// Names that contain a path separator after the last colon (for example "hf.co/org/model") are treated as untagged.
        /// </summary>
        /// <param name="model">Model name. Null or whitespace returns an empty string.</param>
        /// <returns>Normalized model name, never null.</returns>
        public static string Normalize(string? model)
        {
            string trimmed = (model ?? String.Empty).Trim();
            if (trimmed.Length == 0) return String.Empty;
            int colon = trimmed.LastIndexOf(':');
            int slash = trimmed.LastIndexOf('/');
            if (colon > 0 && colon > slash && colon < trimmed.Length - 1) return trimmed;
            if (colon == trimmed.Length - 1) trimmed = trimmed.Substring(0, trimmed.Length - 1);
            return trimmed + LatestTag;
        }

        /// <summary>
        /// Compare two model names after normalization, ignoring case.
        /// </summary>
        /// <param name="left">First model name. May be null.</param>
        /// <param name="right">Second model name. May be null.</param>
        /// <returns>True when both names normalize to the same non-empty value.</returns>
        public static bool AreEqual(string? left, string? right)
        {
            string a = Normalize(left);
            if (a.Length == 0) return false;
            return String.Equals(a, Normalize(right), StringComparison.OrdinalIgnoreCase);
        }
    }
}

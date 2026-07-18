namespace Test.Shared
{
    /// <summary>
    /// Xeno client CLI model list item used by tests.
    /// </summary>
    public class TestCliModelRecord
    {
        /// <summary>
        /// Model name.
        /// </summary>
        public string? Name { get; set; } = null;

        /// <summary>
        /// Whether completions are supported.
        /// </summary>
        public bool SupportsCompletions { get; set; } = false;

        /// <summary>
        /// Whether embeddings are supported.
        /// </summary>
        public bool SupportsEmbeddings { get; set; } = false;

        /// <summary>
        /// Whether server-side model management is allowed for this model provider.
        /// </summary>
        public bool ModelManagementAllowed { get; set; } = false;
    }
}

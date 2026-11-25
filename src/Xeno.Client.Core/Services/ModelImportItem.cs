namespace Xeno.Client.Services
{
    using System;

    /// <summary>
    /// One model to advertise during an import from a provider.
    /// </summary>
    public sealed class ModelImportItem
    {
        /// <summary>
        /// Provider name.
        /// </summary>
        public string Provider { get; set; } = String.Empty;

        /// <summary>
        /// Model name as the provider reports it.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Whether the model serves completions.
        /// </summary>
        public bool SupportsCompletions { get; set; } = true;

        /// <summary>
        /// Whether the model serves embeddings.
        /// </summary>
        public bool SupportsEmbeddings { get; set; } = false;
    }
}

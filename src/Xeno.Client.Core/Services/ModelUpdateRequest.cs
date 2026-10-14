namespace Xeno.Client.Services
{
    using System;

    /// <summary>
    /// Partial update for a configured model. Null values leave the existing value unchanged.
    /// </summary>
    public class ModelUpdateRequest
    {
        /// <summary>
        /// Provider that currently owns the model. Null or empty searches every provider and requires the model name to be unique.
        /// </summary>
        public string? Provider { get; set; } = null;

        /// <summary>
        /// Current model name. Required.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Provider to move the model to, or null to keep it on its current provider.
        /// </summary>
        public string? NewProvider { get; set; } = null;

        /// <summary>
        /// New model name, or null to keep the current name.
        /// </summary>
        public string? NewName { get; set; } = null;

        /// <summary>
        /// Whether the model serves completions, or null to keep the current value.
        /// </summary>
        public bool? SupportsCompletions { get; set; } = null;

        /// <summary>
        /// Whether the model serves embeddings, or null to keep the current value.
        /// </summary>
        public bool? SupportsEmbeddings { get; set; } = null;
    }
}

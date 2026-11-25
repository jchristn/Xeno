namespace Xeno.Client.Services
{
    using System;

    /// <summary>
    /// One model a provider reports it has (for example one row of 'ollama list').
    /// </summary>
    public sealed class ProviderInventoryModel
    {
        /// <summary>
        /// Model name as the provider reports it.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Size on disk in bytes, when the provider reports it.
        /// </summary>
        public long? SizeBytes { get; set; } = null;

        /// <summary>
        /// When the provider last changed the model, as the provider reported it.
        /// </summary>
        public string? Modified { get; set; } = null;

        /// <summary>
        /// Model family, for example "llama".
        /// </summary>
        public string? Family { get; set; } = null;

        /// <summary>
        /// Parameter size, for example "8.0B".
        /// </summary>
        public string? ParameterSize { get; set; } = null;

        /// <summary>
        /// Quantization level, for example "Q4_K_M".
        /// </summary>
        public string? Quantization { get; set; } = null;

        /// <summary>
        /// Content digest.
        /// </summary>
        public string? Digest { get; set; } = null;

        /// <summary>
        /// True when the model runs on a remote host (an Ollama cloud model) rather than on this provider's disk.
        /// </summary>
        public bool Remote { get; set; } = false;

        /// <summary>
        /// True when the provider's Xeno configuration already advertises this model.
        /// </summary>
        public bool Advertised { get; set; } = false;

        /// <summary>
        /// Best guess at whether the model serves embeddings rather than completions, used to prefill imports.
        /// </summary>
        public bool LooksLikeEmbeddings { get; set; } = false;
    }
}

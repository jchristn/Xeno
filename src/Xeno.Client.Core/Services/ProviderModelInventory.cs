namespace Xeno.Client.Services
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The models one provider reports, read live from the provider.
    /// </summary>
    public sealed class ProviderModelInventory
    {
        /// <summary>
        /// Provider name.
        /// </summary>
        public string Provider { get; set; } = String.Empty;

        /// <summary>
        /// Provider API type.
        /// </summary>
        public string ApiType { get; set; } = String.Empty;

        /// <summary>
        /// Provider endpoint.
        /// </summary>
        public string Endpoint { get; set; } = String.Empty;

        /// <summary>
        /// True when the provider answered its model list.
        /// </summary>
        public bool Reachable { get; set; } = false;

        /// <summary>
        /// Why the model list could not be read, when it could not.
        /// </summary>
        public string? Error { get; set; } = null;

        /// <summary>
        /// True when Xeno can pull models onto this provider (Ollama).
        /// </summary>
        public bool CanPull { get; set; } = false;

        /// <summary>
        /// True when Xeno can delete models from this provider (Ollama).
        /// </summary>
        public bool CanDelete { get; set; } = false;

        /// <summary>
        /// UTC time the list was read.
        /// </summary>
        public DateTime CheckedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Models the provider reported, sorted by name.
        /// </summary>
        public List<ProviderInventoryModel> Models { get; set; } = new List<ProviderInventoryModel>();
    }
}

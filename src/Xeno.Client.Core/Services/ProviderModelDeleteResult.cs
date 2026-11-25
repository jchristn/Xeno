namespace Xeno.Client.Services
{
    using System;

    /// <summary>
    /// Outcome of deleting a model from a provider.
    /// </summary>
    public sealed class ProviderModelDeleteResult
    {
        /// <summary>
        /// Provider name.
        /// </summary>
        public string Provider { get; set; } = String.Empty;

        /// <summary>
        /// Model deleted.
        /// </summary>
        public string Model { get; set; } = String.Empty;

        /// <summary>
        /// Advertisements removed along with the model.
        /// </summary>
        public int AdvertisementsRemoved { get; set; } = 0;
    }
}

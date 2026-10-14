namespace Xeno.Client.Presentation
{
    using System;
    using System.Globalization;
    using System.Linq;
    using Xeno.Client.Services;

    /// <summary>
    /// A provider endpoint formatted for tables.
    /// </summary>
    public sealed class ProviderRow
    {
        /// <summary>
        /// Provider name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// API type.
        /// </summary>
        public string ApiType { get; set; } = String.Empty;

        /// <summary>
        /// Endpoint URL.
        /// </summary>
        public string Endpoint { get; set; } = String.Empty;

        /// <summary>
        /// Number of configured models.
        /// </summary>
        public int ModelCount { get; set; } = 0;

        /// <summary>
        /// Comma-separated model names.
        /// </summary>
        public string ModelNames { get; set; } = String.Empty;

        /// <summary>
        /// Timeout, for example "300 s".
        /// </summary>
        public string Timeout { get; set; } = String.Empty;

        /// <summary>
        /// "yes" when the Xeno server may manage models on the provider.
        /// </summary>
        public string ModelManagement { get; set; } = String.Empty;

        /// <summary>
        /// Masked credential.
        /// </summary>
        public string Credential { get; set; } = String.Empty;

        /// <summary>
        /// Source settings.
        /// </summary>
        public ClientRunnerSettings Source { get; set; } = new ClientRunnerSettings();

        /// <summary>
        /// Build a row.
        /// </summary>
        /// <param name="provider">Provider.</param>
        /// <returns>Row.</returns>
        /// <exception cref="ArgumentNullException">Provider is null.</exception>
        public static ProviderRow From(ClientRunnerSettings provider)
        {
            ArgumentNullException.ThrowIfNull(provider);
            return new ProviderRow
            {
                Name = provider.Name,
                ApiType = provider.ApiType,
                Endpoint = provider.Endpoint,
                ModelCount = provider.Models.Count,
                ModelNames = String.Join(", ", provider.Models.Select(item => item.Name)),
                Timeout = (provider.TimeoutMs / 1000.0).ToString("0.#", CultureInfo.InvariantCulture) + " s",
                ModelManagement = DisplayFormat.YesNo(ProviderConfiguration.IsModelManagementAllowed(provider)),
                Credential = String.IsNullOrWhiteSpace(provider.Credential) ? "(none)" : DisplayFormat.Mask(provider.Credential),
                Source = provider
            };
        }
    }
}

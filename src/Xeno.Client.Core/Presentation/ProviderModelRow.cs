namespace Xeno.Client.Presentation
{
    using System;
    using Xeno.Client.Services;

    /// <summary>
    /// A model configured on a provider, formatted for tables.
    /// </summary>
    public sealed class ProviderModelRow
    {
        /// <summary>
        /// Provider name.
        /// </summary>
        public string Provider { get; set; } = String.Empty;

        /// <summary>
        /// Model name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Capabilities.
        /// </summary>
        public string Capabilities { get; set; } = String.Empty;

        /// <summary>
        /// Build a row.
        /// </summary>
        /// <param name="provider">Provider.</param>
        /// <param name="model">Model.</param>
        /// <returns>Row.</returns>
        /// <exception cref="ArgumentNullException">Provider or model is null.</exception>
        public static ProviderModelRow From(ClientRunnerSettings provider, ClientModelSettings model)
        {
            ArgumentNullException.ThrowIfNull(provider);
            ArgumentNullException.ThrowIfNull(model);
            return new ProviderModelRow
            {
                Provider = provider.Name,
                Name = model.Name,
                Capabilities = DisplayFormat.Capabilities(model.SupportsCompletions, model.SupportsEmbeddings)
            };
        }
    }
}

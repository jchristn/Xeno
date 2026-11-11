namespace Xeno.Client.Presentation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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
        /// True when the model is configured in client.json; false when the provider reported it but it is not configured.
        /// </summary>
        public bool Configured { get; set; } = true;

        /// <summary>
        /// Where the model comes from: "Configured", "Found on provider", or both.
        /// </summary>
        public string Source { get; set; } = "Configured";

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
                Capabilities = DisplayFormat.Capabilities(model.SupportsCompletions, model.SupportsEmbeddings),
                Configured = true,
                Source = "Configured"
            };
        }

        /// <summary>
        /// Rows for every model a provider serves: the models configured in client.json, then models the provider reported
        /// (for example 'ollama list') that are not configured. Names are compared the way routing compares them.
        /// </summary>
        /// <param name="provider">Provider.</param>
        /// <param name="discovered">Model names the provider reported, or null.</param>
        /// <returns>Rows.</returns>
        /// <exception cref="ArgumentNullException">provider is null.</exception>
        public static List<ProviderModelRow> ForProvider(ClientRunnerSettings provider, IEnumerable<string>? discovered)
        {
            ArgumentNullException.ThrowIfNull(provider);
            List<string> found = (discovered ?? Enumerable.Empty<string>()).Where(name => !String.IsNullOrWhiteSpace(name)).ToList();
            HashSet<string> foundKeys = new HashSet<string>(found.Select(Services.LocalModelNameNormalizer.Normalize), StringComparer.OrdinalIgnoreCase);
            List<ProviderModelRow> rows = new List<ProviderModelRow>();
            HashSet<string> configured = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ClientModelSettings model in provider.Models)
            {
                ProviderModelRow row = From(provider, model);
                string key = Services.LocalModelNameNormalizer.Normalize(model.Name);
                configured.Add(key);
                if (foundKeys.Contains(key)) row.Source = "Configured, found on provider";
                else if (foundKeys.Count > 0) row.Source = "Configured, not found on provider";
                rows.Add(row);
            }

            foreach (string name in found.OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
            {
                if (!configured.Add(Services.LocalModelNameNormalizer.Normalize(name))) continue;
                rows.Add(new ProviderModelRow
                {
                    Provider = provider.Name,
                    Name = name,
                    Capabilities = "Not configured",
                    Configured = false,
                    Source = "Found on provider"
                });
            }

            return rows;
        }
    }
}

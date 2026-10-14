namespace Xeno.Client.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Builds the unified <see cref="ModelCatalog"/> from the configured providers and, optionally, the Xeno server's
    /// model list. Front ends use one catalog so the local and network views stay consistent. Thread-safe.
    /// </summary>
    public static class ModelCatalogBuilder
    {
        /// <summary>
        /// Build a catalog from configured providers only.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <returns>Catalog with only local entries.</returns>
        /// <exception cref="ArgumentNullException">Settings is null.</exception>
        public static ModelCatalog BuildLocal(ClientSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            return Merge(settings, null, "The network model list was not requested.");
        }

        /// <summary>
        /// Build a catalog from configured providers and the network model list.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="server">Server client used to list network models.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Catalog. When the network list fails, local entries are still returned with a message.</returns>
        /// <exception cref="ArgumentNullException">Settings or server is null.</exception>
        public static async Task<ModelCatalog> BuildAsync(ClientSettings settings, XenoServerClient server, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(server);
            NetworkModelListResult network = await server.ListNetworkModelsAsync(settings, token).ConfigureAwait(false);
            return Merge(settings, network.Success ? network.Models : null, network.Success ? String.Empty : (network.Error ?? "The network model list is unavailable."));
        }

        /// <summary>
        /// Merge configured providers with a network model list.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="networkModels">Network models, or null when unavailable.</param>
        /// <param name="networkMessage">Message explaining why the network list is unavailable.</param>
        /// <returns>Catalog.</returns>
        /// <exception cref="ArgumentNullException">Settings is null.</exception>
        public static ModelCatalog Merge(ClientSettings settings, IEnumerable<NetworkModelEntry>? networkModels, string networkMessage)
        {
            ArgumentNullException.ThrowIfNull(settings);
            Dictionary<string, ModelCatalogEntry> entries = new Dictionary<string, ModelCatalogEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (ClientRunnerSettings provider in settings.Runners)
            {
                foreach (ClientModelSettings model in provider.Models)
                {
                    if (String.IsNullOrWhiteSpace(model.Name)) continue;
                    if (!entries.TryGetValue(model.Name, out ModelCatalogEntry? entry))
                    {
                        entry = new ModelCatalogEntry { Name = model.Name, LocalApiType = provider.ApiType };
                        entries[model.Name] = entry;
                    }

                    entry.ServedLocally = true;
                    if (!entry.LocalProviders.Contains(provider.Name, StringComparer.OrdinalIgnoreCase)) entry.LocalProviders.Add(provider.Name);
                    entry.SupportsCompletions |= model.SupportsCompletions;
                    entry.SupportsEmbeddings |= model.SupportsEmbeddings;
                    entry.ModelManagementAllowed |= ProviderConfiguration.IsModelManagementAllowed(provider);
                }
            }

            if (networkModels != null)
            {
                foreach (NetworkModelEntry network in networkModels)
                {
                    if (String.IsNullOrWhiteSpace(network.Id)) continue;
                    if (!entries.TryGetValue(network.Id, out ModelCatalogEntry? entry))
                    {
                        entry = new ModelCatalogEntry { Name = network.Id };
                        entries[network.Id] = entry;
                    }

                    entry.OnNetwork = true;
                    entry.NetworkOwner = network.OwnedBy ?? String.Empty;
                }
            }

            foreach (ModelCatalogEntry entry in entries.Values)
            {
                entry.Availability = entry.ServedLocally && entry.OnNetwork
                    ? ModelAvailabilityEnum.Both
                    : entry.OnNetwork ? ModelAvailabilityEnum.XenoNetwork : ModelAvailabilityEnum.ThisMachine;
            }

            return new ModelCatalog
            {
                Entries = entries.Values.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToList(),
                NetworkAvailable = networkModels != null,
                NetworkMessage = networkModels != null ? String.Empty : networkMessage,
                BuiltUtc = DateTime.UtcNow
            };
        }
    }
}

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
            return Merge(settings, null, "The network model list was not requested.", Routing.LocalInferenceRuntimeStatus.Load()?.Backends);
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
            return Merge(settings, network.Success ? network.Models : null, network.Success ? String.Empty : (network.Error ?? "The network model list is unavailable."), Routing.LocalInferenceRuntimeStatus.Load()?.Backends);
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
            return Merge(settings, networkModels, networkMessage, null);
        }

        /// <summary>
        /// Merge configured models, models local providers reported, and the network model list into one catalog, and
        /// record where the local inference listener would route each model.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="networkModels">Network models, or null when the list is unavailable.</param>
        /// <param name="networkMessage">Why the network list is unavailable, shown when networkModels is null.</param>
        /// <param name="backends">Local provider health snapshots with discovered models. Null when unknown.</param>
        /// <returns>Catalog.</returns>
        /// <exception cref="ArgumentNullException">settings is null.</exception>
        public static ModelCatalog Merge(ClientSettings settings, IEnumerable<NetworkModelEntry>? networkModels, string networkMessage, IReadOnlyCollection<Routing.LocalBackendStatus>? backends)
        {
            ArgumentNullException.ThrowIfNull(settings);
            Dictionary<string, ModelCatalogEntry> entries = new Dictionary<string, ModelCatalogEntry>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, bool> shared = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (ClientRunnerSettings provider in settings.Runners)
            {
                foreach (ClientModelSettings model in provider.Models)
                {
                    if (String.IsNullOrWhiteSpace(model.Name)) continue;
                    string key = LocalModelNameNormalizer.Normalize(model.Name);
                    if (!entries.TryGetValue(key, out ModelCatalogEntry? entry))
                    {
                        entry = new ModelCatalogEntry { Name = model.Name, LocalApiType = provider.ApiType };
                        entries[key] = entry;
                    }

                    shared[key] = (shared.TryGetValue(key, out bool already) && already) || provider.ShareWithNetwork;

                    entry.ServedLocally = true;
                    if (!entry.LocalProviders.Contains(provider.Name, StringComparer.OrdinalIgnoreCase)) entry.LocalProviders.Add(provider.Name);
                    entry.SupportsCompletions |= model.SupportsCompletions;
                    entry.SupportsEmbeddings |= model.SupportsEmbeddings;
                    entry.ModelManagementAllowed |= ProviderConfiguration.IsModelManagementAllowed(provider);
                }
            }

            if ((settings.LocalInferenceServer?.DiscoverLocalModels ?? true) && backends != null)
            {
                foreach (Routing.LocalBackendStatus status in backends)
                {
                    ClientRunnerSettings? provider = settings.Runners.FirstOrDefault(item => String.Equals(item.Name, status.Provider, StringComparison.OrdinalIgnoreCase));
                    if (provider == null || !provider.RouteLocalRequests) continue;
                    foreach (string name in status.DiscoveredModels)
                    {
                        if (String.IsNullOrWhiteSpace(name)) continue;
                        string key = LocalModelNameNormalizer.Normalize(name);
                        if (entries.TryGetValue(key, out ModelCatalogEntry? existing))
                        {
                            if (existing.ServedLocally && !existing.LocalProviders.Contains(provider.Name, StringComparer.OrdinalIgnoreCase)) existing.LocalProviders.Add(provider.Name);
                            continue;
                        }

                        entries[key] = new ModelCatalogEntry
                        {
                            Name = name,
                            LocalApiType = provider.ApiType,
                            ServedLocally = true,
                            LocalProviders = new List<string> { provider.Name },
                            SupportsCompletions = true,
                            Discovered = true
                        };
                        shared[key] = false;
                    }
                }
            }

            if (networkModels != null)
            {
                foreach (NetworkModelEntry network in networkModels)
                {
                    if (String.IsNullOrWhiteSpace(network.Id)) continue;
                    string key = LocalModelNameNormalizer.Normalize(network.Id);
                    if (!entries.TryGetValue(key, out ModelCatalogEntry? entry))
                    {
                        entry = new ModelCatalogEntry { Name = network.Id };
                        entries[key] = entry;
                    }

                    entry.OnNetwork = true;
                    entry.NetworkOwner = network.OwnedBy ?? String.Empty;
                }
            }

            foreach (KeyValuePair<string, ModelCatalogEntry> pair in entries)
            {
                ModelCatalogEntry entry = pair.Value;
                entry.Availability = entry.ServedLocally && entry.OnNetwork
                    ? ModelAvailabilityEnum.Both
                    : entry.OnNetwork ? ModelAvailabilityEnum.XenoNetwork : ModelAvailabilityEnum.ThisMachine;
                entry.LocalOnly = entry.ServedLocally && !(shared.TryGetValue(pair.Key, out bool isShared) && isShared);
                bool embeddingsOnly = entry.SupportsEmbeddings && !entry.SupportsCompletions;
                Routing.LocalInferenceRouteDecision decision = Routing.LocalInferenceRouter.Resolve(settings, backends, entry.Name, Routing.LocalInferenceApiShapeEnum.OpenAi, embeddingsOnly);
                entry.RouteTarget = decision.RouteHeader;
                entry.RouteReason = decision.Reason;
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

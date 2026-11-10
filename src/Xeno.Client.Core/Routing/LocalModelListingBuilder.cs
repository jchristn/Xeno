namespace Xeno.Client.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Xeno.Client.Services;

    /// <summary>
    /// Builds the merged model listings the local inference listener returns: models served on this machine first, then
    /// the Xeno network's models that this machine does not serve. Names are compared after Ollama-style normalization.
    /// Thread safe; the class is stateless.
    /// </summary>
    public static class LocalModelListingBuilder
    {
        /// <summary>
        /// OpenAI-shaped entries for every model a routable, not-unhealthy provider serves: configured models plus, when
        /// discovery is on, the models it last reported.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="backends">Latest provider health snapshots. Null is treated as empty.</param>
        /// <returns>Local models, never null.</returns>
        /// <exception cref="ArgumentNullException">settings is null.</exception>
        public static List<LocalOpenAiModel> LocalOpenAiModels(ClientSettings settings, IReadOnlyCollection<LocalBackendStatus>? backends)
        {
            ArgumentNullException.ThrowIfNull(settings);
            IReadOnlyCollection<LocalBackendStatus> statuses = backends ?? Array.Empty<LocalBackendStatus>();
            bool discover = settings.LocalInferenceServer?.DiscoverLocalModels ?? true;
            List<LocalOpenAiModel> models = new List<LocalOpenAiModel>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ClientRunnerSettings provider in LocalInferenceRouter.RoutableProviders(settings))
            {
                LocalBackendStatus? status = statuses.FirstOrDefault(item => String.Equals(item.Provider, provider.Name, StringComparison.OrdinalIgnoreCase));
                if (status != null && status.Healthy == false) continue;
                IEnumerable<string> names = (provider.Models ?? new List<ClientModelSettings>()).Where(item => item != null).Select(item => item.Name);
                if (discover && status != null) names = names.Concat(status.DiscoveredModels);
                foreach (string name in names)
                {
                    if (String.IsNullOrWhiteSpace(name)) continue;
                    if (!seen.Add(LocalModelNameNormalizer.Normalize(name))) continue;
                    models.Add(new LocalOpenAiModel { Id = name, OwnedBy = provider.Name });
                }
            }

            return models;
        }

        /// <summary>
        /// Merge local and network OpenAI model lists. Local entries win when both have the same model.
        /// </summary>
        /// <param name="local">Local models. Null is treated as empty.</param>
        /// <param name="network">Network models. Null is treated as empty.</param>
        /// <returns>Merged list, never null.</returns>
        public static LocalOpenAiModelList MergeOpenAi(IEnumerable<LocalOpenAiModel>? local, IEnumerable<LocalOpenAiModel>? network)
        {
            LocalOpenAiModelList merged = new LocalOpenAiModelList();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (LocalOpenAiModel model in (local ?? Enumerable.Empty<LocalOpenAiModel>()).Concat(network ?? Enumerable.Empty<LocalOpenAiModel>()))
            {
                if (model == null || String.IsNullOrWhiteSpace(model.Id)) continue;
                if (!seen.Add(LocalModelNameNormalizer.Normalize(model.Id))) continue;
                merged.Data.Add(model);
            }

            return merged;
        }

        /// <summary>
        /// Keep only the models a provider's /api/tags reported that the router may use: all of them when discovery is on,
        /// otherwise only the configured ones.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="provider">Provider the tags came from.</param>
        /// <param name="tags">The provider's /api/tags response. Null is treated as empty.</param>
        /// <returns>Routable models, never null.</returns>
        /// <exception cref="ArgumentNullException">settings or provider is null.</exception>
        public static List<LocalOllamaModel> RoutableOllamaModels(ClientSettings settings, ClientRunnerSettings provider, LocalOllamaTagsResponse? tags)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(provider);
            bool discover = settings.LocalInferenceServer?.DiscoverLocalModels ?? true;
            List<LocalOllamaModel> models = (tags?.Models ?? new List<LocalOllamaModel>()).Where(item => item != null).ToList();
            if (discover) return models;
            return models.Where(item => (provider.Models ?? new List<ClientModelSettings>()).Any(configured => LocalModelNameNormalizer.AreEqual(configured.Name, String.IsNullOrWhiteSpace(item.Name) ? item.Model : item.Name))).ToList();
        }

        /// <summary>
        /// Merge Ollama model lists in order. Earlier lists win when two contain the same model, so pass local lists first.
        /// </summary>
        /// <param name="lists">Lists in priority order. Null lists and entries are skipped.</param>
        /// <returns>Merged list, never null.</returns>
        public static LocalOllamaTagsResponse MergeOllamaTags(IEnumerable<IEnumerable<LocalOllamaModel>?> lists)
        {
            LocalOllamaTagsResponse merged = new LocalOllamaTagsResponse();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (IEnumerable<LocalOllamaModel>? list in lists ?? Enumerable.Empty<IEnumerable<LocalOllamaModel>?>())
            {
                foreach (LocalOllamaModel model in list ?? Enumerable.Empty<LocalOllamaModel>())
                {
                    if (model == null) continue;
                    string name = String.IsNullOrWhiteSpace(model.Name) ? model.Model : model.Name;
                    if (String.IsNullOrWhiteSpace(name)) continue;
                    if (!seen.Add(LocalModelNameNormalizer.Normalize(name))) continue;
                    if (String.IsNullOrWhiteSpace(model.Name)) model.Name = name;
                    if (String.IsNullOrWhiteSpace(model.Model)) model.Model = name;
                    merged.Models.Add(model);
                }
            }

            return merged;
        }

        /// <summary>
        /// Merge Ollama /api/ps lists from local providers. Network models are never included: they are not loaded on this machine.
        /// </summary>
        /// <param name="lists">Lists from local Ollama providers. Null lists and entries are skipped.</param>
        /// <returns>Merged list, never null.</returns>
        public static LocalOllamaPsResponse MergeOllamaPs(IEnumerable<IEnumerable<LocalOllamaRunningModel>?> lists)
        {
            LocalOllamaPsResponse merged = new LocalOllamaPsResponse();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (IEnumerable<LocalOllamaRunningModel>? list in lists ?? Enumerable.Empty<IEnumerable<LocalOllamaRunningModel>?>())
            {
                foreach (LocalOllamaRunningModel model in list ?? Enumerable.Empty<LocalOllamaRunningModel>())
                {
                    if (model == null) continue;
                    string name = String.IsNullOrWhiteSpace(model.Name) ? model.Model : model.Name;
                    if (String.IsNullOrWhiteSpace(name) || !seen.Add(LocalModelNameNormalizer.Normalize(name))) continue;
                    merged.Models.Add(model);
                }
            }

            return merged;
        }
    }
}

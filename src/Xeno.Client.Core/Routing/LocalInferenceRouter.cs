namespace Xeno.Client.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Xeno.Client.Services;

    /// <summary>
    /// Decides where a local inference request goes: a provider on this machine or the Xeno network.
    /// The CLI, the local dashboard, the desktop app, the terminal app, and the listener all ask this class, so every surface gives the same answer.
    /// Thread safe; the class is stateless and reads only its arguments.
    /// </summary>
    public static class LocalInferenceRouter
    {
        /// <summary>
        /// Resolve the route for a model.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="backends">Latest provider health snapshots. Null is treated as empty (all providers unknown).</param>
        /// <param name="model">Requested model. Null or empty yields an unavailable decision with reason "noModel".</param>
        /// <param name="shape">API shape the caller used.</param>
        /// <param name="embeddings">Whether the request is an embeddings request.</param>
        /// <returns>Decision, never null.</returns>
        /// <exception cref="ArgumentNullException">settings is null.</exception>
        public static LocalInferenceRouteDecision Resolve(ClientSettings settings, IReadOnlyCollection<LocalBackendStatus>? backends, string? model, LocalInferenceApiShapeEnum shape, bool embeddings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ClientLocalInferenceServerSettings listener = settings.LocalInferenceServer ?? new ClientLocalInferenceServerSettings();
            IReadOnlyCollection<LocalBackendStatus> statuses = backends ?? Array.Empty<LocalBackendStatus>();
            LocalInferenceRouteDecision decision = new LocalInferenceRouteDecision
            {
                Model = model ?? String.Empty,
                NormalizedModel = LocalModelNameNormalizer.Normalize(model)
            };

            if (decision.NormalizedModel.Length == 0)
            {
                decision.Target = LocalInferenceRouteTargetKindEnum.Unavailable;
                decision.Reason = "noModel";
                return decision;
            }

            if (listener.RoutingMode == LocalInferenceRoutingModeEnum.NetworkOnly)
            {
                decision.Target = LocalInferenceRouteTargetKindEnum.Network;
                decision.Reason = "networkOnlyMode";
                return decision;
            }

            LocalInferenceRouteRule? rule = listener.Routes.FirstOrDefault(item => item != null && LocalModelNameNormalizer.AreEqual(item.Model, model));
            if (rule != null) return ResolveRule(settings, listener, statuses, decision, rule, shape);

            List<ClientRunnerSettings> candidates = LocalCandidates(settings, listener, statuses, decision.NormalizedModel, embeddings);
            if (candidates.Count > 0)
            {
                ClientRunnerSettings chosen = Choose(candidates, statuses, shape);
                decision.Target = LocalInferenceRouteTargetKindEnum.Local;
                decision.Provider = chosen;
                decision.ShapeSupported = SupportsShape(chosen, shape);
                decision.Reason = IsUnhealthy(chosen, statuses) ? "localUnhealthy" : "preferLocal";
                return decision;
            }

            if (listener.RoutingMode == LocalInferenceRoutingModeEnum.LocalOnly)
            {
                decision.Target = LocalInferenceRouteTargetKindEnum.Unavailable;
                decision.Reason = "localOnlyMode";
                return decision;
            }

            decision.Target = LocalInferenceRouteTargetKindEnum.Network;
            decision.Reason = "notLocal";
            return decision;
        }

        /// <summary>
        /// Whether a provider accepts requests in the given API shape without translation. Ollama accepts both shapes; other providers accept OpenAI only.
        /// </summary>
        /// <param name="provider">Provider.</param>
        /// <param name="shape">API shape.</param>
        /// <returns>True when the provider speaks the shape.</returns>
        /// <exception cref="ArgumentNullException">provider is null.</exception>
        public static bool SupportsShape(ClientRunnerSettings provider, LocalInferenceApiShapeEnum shape)
        {
            ArgumentNullException.ThrowIfNull(provider);
            if (shape == LocalInferenceApiShapeEnum.OpenAi) return true;
            return ProviderConfiguration.IsOllamaApiType(provider.ApiType);
        }

        /// <summary>
        /// Providers the listener may route to: those with RouteLocalRequests set, in configured order.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <returns>Routable providers.</returns>
        /// <exception cref="ArgumentNullException">settings is null.</exception>
        public static List<ClientRunnerSettings> RoutableProviders(ClientSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            return (settings.Providers ?? new List<ClientRunnerSettings>()).Where(item => item != null && item.RouteLocalRequests).ToList();
        }

        /// <summary>
        /// Whether a provider serves a model, by its configured models or (when discovery is on) the models it last reported.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="provider">Provider.</param>
        /// <param name="backends">Latest provider health snapshots. Null is treated as empty.</param>
        /// <param name="model">Model name.</param>
        /// <param name="embeddings">Whether the request is an embeddings request. Configured models must declare embeddings support.</param>
        /// <returns>True when the provider serves the model.</returns>
        /// <exception cref="ArgumentNullException">settings or provider is null.</exception>
        public static bool Serves(ClientSettings settings, ClientRunnerSettings provider, IReadOnlyCollection<LocalBackendStatus>? backends, string? model, bool embeddings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(provider);
            ClientModelSettings? configured = (provider.Models ?? new List<ClientModelSettings>()).FirstOrDefault(item => item != null && LocalModelNameNormalizer.AreEqual(item.Name, model));
            if (configured != null) return embeddings ? configured.SupportsEmbeddings : configured.SupportsCompletions;
            if (!(settings.LocalInferenceServer?.DiscoverLocalModels ?? true)) return false;
            LocalBackendStatus? status = Find(provider, backends ?? Array.Empty<LocalBackendStatus>());
            return status != null && status.DiscoveredModels.Any(item => LocalModelNameNormalizer.AreEqual(item, model));
        }

        private static LocalInferenceRouteDecision ResolveRule(ClientSettings settings, ClientLocalInferenceServerSettings listener, IReadOnlyCollection<LocalBackendStatus> statuses, LocalInferenceRouteDecision decision, LocalInferenceRouteRule rule, LocalInferenceApiShapeEnum shape)
        {
            decision.FallbackToNetwork = rule.FallbackToNetwork && listener.RoutingMode != LocalInferenceRoutingModeEnum.LocalOnly;
            if (rule.Target == LocalInferenceRouteTargetEnum.Network)
            {
                if (listener.RoutingMode == LocalInferenceRoutingModeEnum.LocalOnly)
                {
                    decision.Target = LocalInferenceRouteTargetKindEnum.Unavailable;
                    decision.Reason = "localOnlyMode";
                    return decision;
                }

                decision.Target = LocalInferenceRouteTargetKindEnum.Network;
                decision.Reason = "rule";
                return decision;
            }

            ClientRunnerSettings? provider = RoutableProviders(settings).FirstOrDefault(item => String.Equals(item.Name, rule.Provider, StringComparison.OrdinalIgnoreCase));
            if (provider == null)
            {
                if (decision.FallbackToNetwork)
                {
                    decision.Target = LocalInferenceRouteTargetKindEnum.Network;
                    decision.Reason = "ruleProviderUnavailable";
                    decision.IsFallback = true;
                    return decision;
                }

                decision.Target = LocalInferenceRouteTargetKindEnum.Unavailable;
                decision.Reason = "ruleProviderUnavailable";
                return decision;
            }

            decision.Target = LocalInferenceRouteTargetKindEnum.Local;
            decision.Provider = provider;
            decision.ShapeSupported = SupportsShape(provider, shape);
            decision.Reason = "rule";
            if (IsUnhealthy(provider, statuses) && decision.FallbackToNetwork)
            {
                decision.Target = LocalInferenceRouteTargetKindEnum.Network;
                decision.Provider = null;
                decision.ShapeSupported = true;
                decision.Reason = "localUnhealthy";
                decision.IsFallback = true;
            }

            return decision;
        }

        private static List<ClientRunnerSettings> LocalCandidates(ClientSettings settings, ClientLocalInferenceServerSettings listener, IReadOnlyCollection<LocalBackendStatus> statuses, string model, bool embeddings)
        {
            List<ClientRunnerSettings> routable = RoutableProviders(settings);
            List<ClientRunnerSettings> configured = routable.Where(item => IsConfigured(item, model, embeddings)).ToList();
            List<ClientRunnerSettings> discovered = routable.Where(item => !configured.Contains(item) && Serves(settings, item, statuses, model, embeddings)).ToList();
            return configured.Concat(discovered).ToList();
        }

        private static bool IsConfigured(ClientRunnerSettings provider, string model, bool embeddings)
        {
            ClientModelSettings? configured = (provider.Models ?? new List<ClientModelSettings>()).FirstOrDefault(item => item != null && LocalModelNameNormalizer.AreEqual(item.Name, model));
            return configured != null && (embeddings ? configured.SupportsEmbeddings : configured.SupportsCompletions);
        }

        // Candidates arrive with providers that have the model configured first, then providers that only reported it,
        // each group in configured order. A gateway can list every model while serving only some of them.
        private static ClientRunnerSettings Choose(List<ClientRunnerSettings> candidates, IReadOnlyCollection<LocalBackendStatus> statuses, LocalInferenceApiShapeEnum shape)
        {
            ClientRunnerSettings? best = candidates.FirstOrDefault(item => !IsUnhealthy(item, statuses) && SupportsShape(item, shape));
            if (best != null) return best;
            best = candidates.FirstOrDefault(item => !IsUnhealthy(item, statuses));
            return best ?? candidates[0];
        }

        private static bool IsUnhealthy(ClientRunnerSettings provider, IReadOnlyCollection<LocalBackendStatus> statuses)
        {
            LocalBackendStatus? status = Find(provider, statuses);
            return status != null && status.Healthy == false;
        }

        private static LocalBackendStatus? Find(ClientRunnerSettings provider, IReadOnlyCollection<LocalBackendStatus> statuses)
        {
            return statuses.FirstOrDefault(item => item != null && String.Equals(item.Provider, provider.Name, StringComparison.OrdinalIgnoreCase));
        }
    }
}

namespace Xeno.Client.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Client.Presentation;
    using Xeno.Client.Services;

    /// <summary>
    /// Builds routing views, resolves routes, and edits route rules for every front end. Reads live provider health and
    /// counters from <see cref="LocalInferenceRuntimeStatus"/>, which the running listener publishes.
    /// Thread safe; the class is stateless.
    /// </summary>
    public static class LocalInferenceRoutingService
    {
        /// <summary>
        /// Build the routing view.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="runtime">Live listener status, or null when the listener is not running.</param>
        /// <returns>View, never null.</returns>
        /// <exception cref="ArgumentNullException">settings is null.</exception>
        public static LocalInferenceRoutingView BuildView(ClientSettings settings, LocalInferenceRuntimeStatus? runtime)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ClientLocalInferenceServerSettings listener = settings.LocalInferenceServer ?? new ClientLocalInferenceServerSettings();
            LocalInferenceRoutingView view = new LocalInferenceRoutingView
            {
                Enabled = listener.Enabled,
                Url = "http://" + listener.Host + ":" + listener.Port.ToString(CultureInfo.InvariantCulture),
                RoutingMode = listener.RoutingMode.ToString(),
                RuntimeAvailable = runtime != null,
                UpdatedUtc = runtime?.UpdatedUtc,
                Rules = listener.Routes.Where(item => item != null).ToList(),
                LocalRequests = runtime?.LocalRequests ?? 0,
                NetworkRequests = runtime?.NetworkRequests ?? 0,
                Fallbacks = runtime?.Fallbacks ?? 0,
                Errors = runtime?.Errors ?? 0,
                DefaultMaxConcurrentRequests = settings.DefaultMaxConcurrentRequests
            };

            foreach (ClientRunnerSettings provider in settings.Providers.Where(item => item != null))
            {
                LocalBackendStatus? status = runtime?.Backends.FirstOrDefault(item => String.Equals(item.Provider, provider.Name, StringComparison.OrdinalIgnoreCase));
                ProviderConcurrencySnapshot? concurrency = runtime?.Concurrency.FirstOrDefault(item => String.Equals(item.Provider, provider.Name, StringComparison.OrdinalIgnoreCase));
                ConcurrencyLimits limits = ConcurrencyLimitResolver.Resolve(settings, provider, null);
                LocalBackendView backend = new LocalBackendView
                {
                    Provider = provider.Name,
                    ApiType = provider.ApiType,
                    Endpoint = provider.Endpoint,
                    Health = !provider.RouteLocalRequests ? "notRoutable" : status?.HealthCode ?? "unknown",
                    LastCheckedUtc = status?.LastCheckedUtc,
                    LatencyMs = status?.LastLatencyMs,
                    LastError = status?.LastError ?? String.Empty,
                    InFlight = concurrency?.InFlight ?? 0,
                    InFlightLocal = concurrency?.InFlightLocal ?? 0,
                    InFlightNetwork = concurrency?.InFlightNetwork ?? 0,
                    Queued = concurrency?.Queued ?? 0,
                    Limit = limits.ProviderLimit,
                    LimitSource = limits.ProviderLimitSource,
                    Shared = provider.ShareWithNetwork,
                    Routable = provider.RouteLocalRequests,
                    DiscoveredModels = status?.DiscoveredModels.Count ?? 0
                };
                view.Backends.Add(backend);
            }

            view.RoutableBackends = view.Backends.Count(item => item.Routable);
            view.HealthyBackends = view.Backends.Count(item => item.Routable && item.Health != "unhealthy");
            return view;
        }

        /// <summary>
        /// Load settings and live status from disk and build the view.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>View.</returns>
        public static Task<LocalInferenceRoutingView> LoadViewAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult(BuildView(ClientSettings.Load(), LocalInferenceRuntimeStatus.Load()));
        }

        /// <summary>
        /// Resolve where a model would go, using live provider health when the listener is running.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="runtime">Live listener status, or null.</param>
        /// <param name="model">Model name.</param>
        /// <param name="shape">API shape. Default OpenAI.</param>
        /// <returns>Decision.</returns>
        /// <exception cref="ArgumentNullException">settings is null.</exception>
        public static LocalInferenceRouteDecision Resolve(ClientSettings settings, LocalInferenceRuntimeStatus? runtime, string? model, LocalInferenceApiShapeEnum shape = LocalInferenceApiShapeEnum.OpenAi)
        {
            ArgumentNullException.ThrowIfNull(settings);
            return LocalInferenceRouter.Resolve(settings, runtime?.Backends, model, shape, false);
        }

        /// <summary>
        /// Add or replace the rule for a model, validating it against the configured providers. The caller saves the settings.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="rule">Rule.</param>
        /// <param name="originalModel">Model of the rule being edited, when renaming an existing rule. Null for a new rule.</param>
        /// <returns>The stored rule.</returns>
        /// <exception cref="ArgumentNullException">settings or rule is null.</exception>
        /// <exception cref="ArgumentException">The rule is invalid.</exception>
        public static LocalInferenceRouteRule SaveRule(ClientSettings settings, LocalInferenceRouteRule rule, string? originalModel = null)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(rule);
            if (rule.Target == LocalInferenceRouteTargetEnum.Network) rule.Provider = null;
            List<LocalInferenceRouteRule> rules = settings.LocalInferenceServer.Routes
                .Where(item => item != null && !LocalModelNameNormalizer.AreEqual(item.Model, rule.Model) && !(originalModel != null && LocalModelNameNormalizer.AreEqual(item.Model, originalModel)))
                .ToList();
            rules.Add(rule);
            LocalInferenceRouteValidator.Validate(rules, settings.Providers);
            settings.LocalInferenceServer.Routes = rules;
            return rule;
        }

        /// <summary>
        /// Remove the rule for a model. The caller saves the settings.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="model">Model.</param>
        /// <returns>Number of rules removed.</returns>
        /// <exception cref="ArgumentNullException">settings is null.</exception>
        public static int DeleteRule(ClientSettings settings, string model)
        {
            ArgumentNullException.ThrowIfNull(settings);
            int before = settings.LocalInferenceServer.Routes.Count;
            settings.LocalInferenceServer.Routes = settings.LocalInferenceServer.Routes.Where(item => item != null && !LocalModelNameNormalizer.AreEqual(item.Model, model)).ToList();
            return before - settings.LocalInferenceServer.Routes.Count;
        }

        /// <summary>
        /// Check every routable provider now from this process. Results are returned, not published; the running listener keeps its own.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Statuses.</returns>
        /// <exception cref="ArgumentNullException">settings is null.</exception>
        public static async Task<IReadOnlyCollection<LocalBackendStatus>> CheckBackendsAsync(ClientSettings settings, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(settings);
            using (LocalBackendHealthMonitor monitor = new LocalBackendHealthMonitor(() => settings))
            {
                return await monitor.CheckNowAsync(token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Convert a decision to the API response shape, without provider settings or credentials.
        /// </summary>
        /// <param name="decision">Decision.</param>
        /// <returns>Response.</returns>
        /// <exception cref="ArgumentNullException">decision is null.</exception>
        public static Xeno.Client.LocalDashboard.Models.LocalRouteResolveResponse ToResponse(LocalInferenceRouteDecision decision)
        {
            ArgumentNullException.ThrowIfNull(decision);
            return new Xeno.Client.LocalDashboard.Models.LocalRouteResolveResponse
            {
                Model = decision.Model,
                NormalizedModel = decision.NormalizedModel,
                Target = decision.Target.ToString().ToLowerInvariant(),
                Route = decision.RouteHeader,
                Provider = decision.ProviderName,
                Reason = decision.Reason,
                ReasonText = DescribeReason(decision.Reason),
                FallbackToNetwork = decision.FallbackToNetwork,
                ShapeSupported = decision.ShapeSupported
            };
        }

        /// <summary>
        /// Human-readable reason for a route reason code.
        /// </summary>
        /// <param name="reason">Reason code.</param>
        /// <returns>Sentence, never null.</returns>
        public static string DescribeReason(string? reason)
        {
            switch (reason)
            {
                case "rule": return "A route rule for this model chose the target.";
                case "preferLocal": return "A provider on this machine serves the model, and routing prefers this machine.";
                case "notLocal": return "No provider on this machine serves the model, so it goes to the Xeno network.";
                case "localUnhealthy": return "The provider on this machine that serves the model is not responding.";
                case "networkOnlyMode": return "Routing is set to Xeno network only.";
                case "localOnlyMode": return "Routing is set to this machine only, and nothing here serves the model.";
                case "ruleProviderUnavailable": return "The route rule names a provider that is missing or not used for local requests.";
                case "noModel": return "No model was given.";
                default: return "Unknown reason.";
            }
        }

        /// <summary>
        /// Markdown explanation of a route decision, with values escaped, for viewers in the desktop and terminal apps.
        /// </summary>
        /// <param name="decision">Decision.</param>
        /// <returns>Markdown.</returns>
        /// <exception cref="ArgumentNullException">decision is null.</exception>
        public static string ExplainMarkdown(LocalInferenceRouteDecision decision)
        {
            ArgumentNullException.ThrowIfNull(decision);
            List<KeyValuePair<string, string>> details = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("Model", decision.Model),
                new KeyValuePair<string, string>("Normalized", decision.NormalizedModel),
                new KeyValuePair<string, string>("Goes to", decision.RouteHeader),
                new KeyValuePair<string, string>("Reason", decision.Reason),
                new KeyValuePair<string, string>("Falls back to the network", decision.FallbackToNetwork ? "yes, if the provider fails before responding" : "no")
            };
            if (decision.Target == LocalInferenceRouteTargetKindEnum.Local && !decision.ShapeSupported)
            {
                details.Add(new KeyValuePair<string, string>("Note", "This provider accepts OpenAI-compatible requests only; call the /v1 routes."));
            }

            return XenoMarkdown.Join(new string?[]
            {
                XenoMarkdown.Heading("Where " + decision.Model + " goes"),
                XenoMarkdown.Escape(DescribeReason(decision.Reason)),
                XenoMarkdown.Details(details)
            });
        }
    }
}

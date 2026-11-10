namespace Xeno.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Caching;
    using Xeno.Core.Database;
    using Xeno.Core.Enums;
    using Xeno.Core.Helpers;
    using Xeno.Core.Models;
    using Xeno.Core.Settings;

    /// <summary>
    /// Routes inference requests directly to connected Xeno clients.
    /// </summary>
    public class RoutingService
    {
        private static readonly object _RandomLock = new object();
        private static readonly object _RoundRobinLock = new object();
        private static readonly Random _Random = new Random();
        private static readonly Dictionary<string, long> _LeastRecentlyUsedSelections = new Dictionary<string, long>(StringComparer.Ordinal);
        private static long _LeastRecentlyUsedSequence = 0;
        private static int _RoundRobinIndex = 0;

        private readonly DatabaseDriverBase _Database;
        private readonly RoutingSettings _Settings;
        private readonly EntityCache<List<RoutingPolicy>> _RoutingPolicyCache;
        private readonly bool _CacheEnabled;

        /// <summary>
        /// Instantiate the routing service.
        /// </summary>
        /// <param name="database">Database driver providing access to the control-plane repositories.</param>
        /// <param name="settings">Routing settings.</param>
        /// <param name="cache">Service-layer cache settings. When null, defaults are used.</param>
        public RoutingService(DatabaseDriverBase database, RoutingSettings? settings = null, CacheSettings? cache = null)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Settings = settings ?? new RoutingSettings();
            CacheSettings cacheSettings = cache ?? new CacheSettings();
            _CacheEnabled = cacheSettings.Enabled;
            _RoutingPolicyCache = new EntityCache<List<RoutingPolicy>>(cacheSettings.RoutingPolicyCapacity, cacheSettings.RoutingPolicyTtlSeconds);
        }

        /// <summary>
        /// Route a request to an eligible connected client.
        /// </summary>
        /// <param name="context">Request context.</param>
        /// <param name="model">Requested model.</param>
        /// <param name="isEmbedding">Whether request is an embedding request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Routing decision.</returns>
        public Task<RoutingDecision> RouteAsync(RequestContext context, string? model, bool isEmbedding, CancellationToken token = default)
        {
            return RouteAsync(context, model, isEmbedding, null, token);
        }

        /// <summary>
        /// Route a request to an eligible connected client.
        /// </summary>
        /// <param name="context">Request context.</param>
        /// <param name="model">Requested model.</param>
        /// <param name="isEmbedding">Whether request is an embedding request.</param>
        /// <param name="provider">Optional upstream provider filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <param name="excludeClientId">Client that must not be selected, for example the client a fallback request came from. Null excludes nothing.</param>
        /// <returns>Routing decision.</returns>
        public async Task<RoutingDecision> RouteAsync(RequestContext context, string? model, bool isEmbedding, ApiProviderTypeEnum? provider, CancellationToken token = default, string? excludeClientId = null)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            token.ThrowIfCancellationRequested();

            RoutingDecision decision = new RoutingDecision
            {
                Success = false,
                OutcomeCode = "Started",
                Message = "Routing started."
            };

            Tenant? consumerTenant = String.IsNullOrEmpty(context.TenantId)
                ? null
                : await _Database.Tenants!.ReadAsync(context.TenantId, token).ConfigureAwait(false);
            bool consumeShared = consumerTenant?.ConsumeShared ?? false;

            List<XenoClientRecord> visibleClients = await LoadVisibleClientsAsync(context.TenantId, consumeShared, token).ConfigureAwait(false);

            string effectiveModel = await ResolveModelAsync(context.TenantId, model, isEmbedding, provider, visibleClients, token).ConfigureAwait(false);
            decision.Timeline.Add("ModelResolution");
            if (String.IsNullOrWhiteSpace(effectiveModel))
            {
                decision.HttpStatusCode = 404;
                decision.OutcomeCode = "ModelNotFound";
                decision.Message = "No advertised model can service the request.";
                return decision;
            }
            decision.EffectiveModel = effectiveModel;

            DateTime utcNow = DateTime.UtcNow;
            DateTime freshnessCutoffUtc = utcNow.AddMilliseconds(-_Settings.ClientFreshnessTimeoutMs);
            List<XenoClientRecord> candidates = visibleClients
                .Where(item => String.IsNullOrWhiteSpace(excludeClientId) || !String.Equals(item.Id, excludeClientId, StringComparison.OrdinalIgnoreCase))
                .Where(item => IsEligibleClient(item, effectiveModel, isEmbedding, provider, freshnessCutoffUtc))
                .OrderBy(item => item.Name)
                .ThenByDescending(item => item.Weight)
                .ThenBy(item => item.Id)
                .ToList();
            decision.ExcludedClientReasons = visibleClients
                .Select(item => BuildClientExclusionReason(item, effectiveModel, isEmbedding, provider, freshnessCutoffUtc))
                .Where(item => !String.IsNullOrWhiteSpace(item))
                .ToList();
            if (!String.IsNullOrWhiteSpace(excludeClientId))
            {
                XenoClientRecord? excluded = visibleClients.FirstOrDefault(item => String.Equals(item.Id, excludeClientId, StringComparison.OrdinalIgnoreCase));
                if (excluded != null) decision.ExcludedClientReasons.Add(ClientLabel(excluded) + ":RequestingClientExcluded");
            }

            decision.Timeline.Add("ClientInventory");

            RoutingPolicy policy = await ResolveRoutingPolicyAsync(context.TenantId, effectiveModel, candidates, token).ConfigureAwait(false);
            ApplyRoutingPolicy(decision, policy);
            decision.Timeline.Add("PolicyResolution");

            List<XenoClientRecord> policyCandidates = FilterCandidatesByPolicy(candidates, policy).ToList();
            decision.ExcludedClientReasons.AddRange(candidates
                .Where(item => !policyCandidates.Any(candidate => String.Equals(candidate.Id, item.Id, StringComparison.OrdinalIgnoreCase)))
                .Select(item => ClientLabel(item) + ":RoutingPolicyClientMismatch:" + (String.IsNullOrWhiteSpace(policy.ClientName) ? "*" : policy.ClientName)));
            candidates = policyCandidates;
            decision.CandidateClientIds = candidates.Select(item => item.Id).ToList();
            decision.CandidateClientNames = candidates.Select(item => item.Name).ToList();
            decision.CandidateCount = candidates.Count;

            if (candidates.Count < 1)
            {
                decision.HttpStatusCode = 502;
                decision.OutcomeCode = "NoEligibleClients";
                decision.Message = "No connected client can service the request.";
                return decision;
            }

            XenoClientRecord selected = SelectClient(candidates, policy.LoadBalancingMode, BuildRouteScopeKey(context, effectiveModel, isEmbedding, policy), decision, policy.Adaptive);
            XenoClientModel? selectedModel = selected.Models.FirstOrDefault(item => ModelMatches(item, effectiveModel, isEmbedding, provider));
            decision.LoadBalancingMode = policy.LoadBalancingMode;
            decision.Success = true;
            decision.HttpStatusCode = 200;
            decision.OutcomeCode = "Routed";
            decision.Message = "Request routed to client '" + selected.Name + "'.";
            decision.SelectedClientId = selected.Id;
            decision.SelectedClientName = selected.Name;
            decision.SelectedProvider = selectedModel?.Provider.ToString();
            decision.SelectedSource = selectedModel?.Source;
            decision.SelectedClientInFlightRequests = selected.InFlightRequests;
            decision.SelectedClientMaxParallelRequests = selected.MaxParallelRequests;
            decision.SelectedClientWeight = selected.Weight;
            decision.SelectedClientLastHeartbeatUtc = selected.LastHeartbeatUtc;
            decision.SelectionReason = BuildSelectionReason(policy.LoadBalancingMode, selected, decision);
            decision.Timeline.Add("ClientSelection");
            return decision;
        }

        /// <summary>
        /// Record a completed request.
        /// </summary>
        /// <param name="context">Request context.</param>
        /// <param name="path">Request path.</param>
        /// <param name="method">HTTP method.</param>
        /// <param name="decision">Routing decision.</param>
        /// <returns>Request history entry.</returns>
        public RequestHistoryEntry RecordHistory(RequestContext context, string path, string method, RoutingDecision decision)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (decision == null) throw new ArgumentNullException(nameof(decision));

            if (String.IsNullOrWhiteSpace(context.RequestId))
            {
                context.RequestId = IdGenerator.NewRequestHistoryId();
            }

            RequestHistoryEntry entry = new RequestHistoryEntry
            {
                Id = context.RequestId,
                TenantId = context.TenantId,
                RequestorTenantId = context.TenantId,
                Path = path,
                Method = method,
                Model = decision.EffectiveModel,
                SelectedClientId = decision.SelectedClientId,
                StatusCode = decision.HttpStatusCode,
                DurationMs = Math.Max(0, (long)(DateTime.UtcNow - context.StartedUtc).TotalMilliseconds),
                SourceIp = context.SourceIp,
                UserId = context.UserId,
                CredentialId = context.CredentialId,
                SessionId = context.SessionId,
                PrincipalType = context.PrincipalType,
                RequestorPrincipalType = context.PrincipalType,
                RequestorPrincipalId = context.PrincipalId,
                AuthScheme = context.AuthScheme,
                RoutingDecision = decision
            };
            return entry;
        }

        /// <summary>
        /// Load the clients visible to a tenant for routing, reading from the client repository. A tenant
        /// always sees its own clients; when the tenant consumes shared clients, active shared clients owned
        /// by other tenants are also included. Clients are read directly (never cached) because their
        /// heartbeat and in-flight state changes frequently.
        /// </summary>
        /// <param name="tenantId">Consuming tenant identifier.</param>
        /// <param name="consumeShared">Whether the consuming tenant consumes shared clients.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Clients visible to the tenant.</returns>
        private async Task<List<XenoClientRecord>> LoadVisibleClientsAsync(string tenantId, bool consumeShared, CancellationToken token)
        {
            List<XenoClientRecord> owned = await _Database.Clients!.EnumerateAsync(tenantId, token).ConfigureAwait(false);
            if (!consumeShared)
            {
                return owned.Where(item => VisibleToTenant(item, tenantId, consumeShared)).ToList();
            }

            List<XenoClientRecord> shared = await _Database.Clients!.EnumerateSharedAsync(token).ConfigureAwait(false);
            Dictionary<string, XenoClientRecord> seenIds = new Dictionary<string, XenoClientRecord>(StringComparer.Ordinal);
            List<XenoClientRecord> combined = new List<XenoClientRecord>();
            foreach (XenoClientRecord client in owned)
            {
                if (client.Id != null)
                {
                    if (seenIds.ContainsKey(client.Id)) continue;
                    seenIds[client.Id] = client;
                }
                combined.Add(client);
            }
            foreach (XenoClientRecord client in shared)
            {
                if (client.Id != null)
                {
                    if (seenIds.ContainsKey(client.Id)) continue;
                    seenIds[client.Id] = client;
                }
                combined.Add(client);
            }

            return combined.Where(item => VisibleToTenant(item, tenantId, consumeShared)).ToList();
        }

        private async Task<string> ResolveModelAsync(string tenantId, string? requestedModel, bool isEmbedding, ApiProviderTypeEnum? provider, List<XenoClientRecord> visibleClients, CancellationToken token)
        {
            List<ModelDefinition> definitions = (await _Database.ModelDefinitions!.EnumerateAsync(tenantId, token).ConfigureAwait(false))
                .Where(item => isEmbedding ? item.SupportsEmbeddings : item.SupportsCompletions)
                .ToList();

            if (!String.IsNullOrWhiteSpace(requestedModel))
            {
                ModelDefinition? requested = definitions.FirstOrDefault(item => String.Equals(item.Name, requestedModel, StringComparison.OrdinalIgnoreCase));
                if (requested != null) return requested.Name;

                bool clientAdvertisesRequestedModel = visibleClients
                    .Any(item => item.Models.Any(clientModel => ModelMatches(clientModel, requestedModel, isEmbedding, provider)));
                return clientAdvertisesRequestedModel ? requestedModel : String.Empty;
            }

            string? definedModel = definitions.FirstOrDefault()?.Name;
            if (!String.IsNullOrWhiteSpace(definedModel)) return definedModel;

            return visibleClients
                .SelectMany(item => item.Models)
                .Where(item => ModelMatches(item, item.Name, isEmbedding, provider))
                .Select(item => item.Name)
                .FirstOrDefault() ?? String.Empty;
        }

        /// <summary>
        /// Determine whether a client is visible to a tenant for routing. A tenant always sees its own clients;
        /// clients owned by other tenants are visible only when the consumer consumes shared clients and the
        /// client is both shared and active.
        /// </summary>
        /// <param name="client">Candidate client.</param>
        /// <param name="consumerTenantId">Consuming tenant identifier.</param>
        /// <param name="consumeShared">Whether the consuming tenant consumes shared clients.</param>
        /// <returns>True when the client is visible to the consuming tenant.</returns>
        private static bool VisibleToTenant(XenoClientRecord client, string consumerTenantId, bool consumeShared)
        {
            if (String.Equals(client.TenantId, consumerTenantId, StringComparison.Ordinal)) return true;
            if (!consumeShared) return false;
            return client.Shared && client.Active;
        }

        private static bool ModelMatches(XenoClientModel model, string? name, bool isEmbedding, ApiProviderTypeEnum? provider)
        {
            if (model == null) return false;
            if (!String.Equals(model.Name, name, StringComparison.OrdinalIgnoreCase)) return false;
            if (provider.HasValue && model.Provider != provider.Value) return false;
            return isEmbedding ? model.SupportsEmbeddings : model.SupportsCompletions;
        }

        private static bool IsEligibleClient(XenoClientRecord client, string effectiveModel, bool isEmbedding, ApiProviderTypeEnum? provider, DateTime freshnessCutoffUtc)
        {
            return String.IsNullOrWhiteSpace(BuildClientExclusionReason(client, effectiveModel, isEmbedding, provider, freshnessCutoffUtc));
        }

        private static string BuildClientExclusionReason(XenoClientRecord client, string effectiveModel, bool isEmbedding, ApiProviderTypeEnum? provider, DateTime freshnessCutoffUtc)
        {
            if (client == null) return "UnknownClient";
            string label = ClientLabel(client);
            if (!client.Active) return label + ":Inactive";
            if (client.ServiceState != ClientServiceStateEnum.Normal) return label + ":ServiceState:" + client.ServiceState;
            if (!client.Connected) return label + ":Disconnected";
            if (client.LastHeartbeatUtc < freshnessCutoffUtc) return label + ":StaleHeartbeat:" + client.LastHeartbeatUtc.ToString("O", CultureInfo.InvariantCulture);

            string modelReason = BuildModelExclusionReason(client, effectiveModel, isEmbedding, provider);
            if (!String.IsNullOrWhiteSpace(modelReason)) return label + ":" + modelReason;

            if (client.MaxParallelRequests > 0 && client.InFlightRequests >= client.MaxParallelRequests)
            {
                return label + ":AtCapacity:" + client.InFlightRequests.ToString(CultureInfo.InvariantCulture) + "/" + client.MaxParallelRequests.ToString(CultureInfo.InvariantCulture);
            }

            return String.Empty;
        }

        private static string BuildModelExclusionReason(XenoClientRecord client, string effectiveModel, bool isEmbedding, ApiProviderTypeEnum? provider)
        {
            List<XenoClientModel> sameName = client.Models
                .Where(item => String.Equals(item.Name, effectiveModel, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (sameName.Count == 0) return "ModelNotAdvertised:" + effectiveModel;

            List<XenoClientModel> sameProvider = provider.HasValue
                ? sameName.Where(item => item.Provider == provider.Value).ToList()
                : sameName;
            if (sameProvider.Count == 0)
            {
                string advertisedProviders = String.Join(",", sameName.Select(item => item.Provider.ToString()).Distinct(StringComparer.OrdinalIgnoreCase));
                return "ProviderMismatch:required=" + provider?.ToString() + ";advertised=" + advertisedProviders;
            }

            bool supportsRequestedMode = isEmbedding
                ? sameProvider.Any(item => item.SupportsEmbeddings)
                : sameProvider.Any(item => item.SupportsCompletions);
            if (!supportsRequestedMode) return isEmbedding ? "CapabilityMismatch:Embeddings" : "CapabilityMismatch:Completions";

            return String.Empty;
        }

        private static string ClientLabel(XenoClientRecord client)
        {
            string id = client.Id ?? String.Empty;
            string name = client.Name ?? String.Empty;
            if (String.IsNullOrWhiteSpace(name)) return id;
            if (String.IsNullOrWhiteSpace(id) || String.Equals(id, name, StringComparison.OrdinalIgnoreCase)) return name;
            return name + "(" + id + ")";
        }

        private XenoClientRecord SelectClient(List<XenoClientRecord> candidates, LoadBalancingModeEnum mode, string routeScopeKey, RoutingDecision decision, AdaptiveLoadBalancingSettings adaptive)
        {
            if (candidates.Count == 1)
            {
                if (mode == LoadBalancingModeEnum.LeastRecentlyUsed) MarkLeastRecentlyUsedSelection(routeScopeKey, candidates[0].Id);
                if (mode == LoadBalancingModeEnum.Adaptive) decision.AdaptiveScore = BuildAdaptiveScore(candidates[0], adaptive).ToString("F2", CultureInfo.InvariantCulture);
                return candidates[0];
            }

            switch (mode)
            {
                case LoadBalancingModeEnum.Random:
                    return SelectWeightedRandom(candidates);
                case LoadBalancingModeEnum.FirstAvailable:
                    return candidates[0];
                case LoadBalancingModeEnum.LeastRecentlyUsed:
                    return SelectLeastRecentlyUsed(candidates, routeScopeKey);
                case LoadBalancingModeEnum.Adaptive:
                    return SelectAdaptive(candidates, decision, adaptive);
                case LoadBalancingModeEnum.RoundRobin:
                default:
                    return SelectWeightedRoundRobin(candidates);
            }
        }

        private static XenoClientRecord SelectWeightedRandom(List<XenoClientRecord> candidates)
        {
            int totalWeight = TotalWeight(candidates);
            int randomValue;
            lock (_RandomLock)
            {
                randomValue = _Random.Next(totalWeight);
            }

            return SelectByWeightBucket(candidates, randomValue);
        }

        private static XenoClientRecord SelectWeightedRoundRobin(List<XenoClientRecord> candidates)
        {
            lock (_RoundRobinLock)
            {
                int totalWeight = TotalWeight(candidates);
                int index = _RoundRobinIndex % totalWeight;
                _RoundRobinIndex++;
                return SelectByWeightBucket(candidates, index);
            }
        }

        private static XenoClientRecord SelectByWeightBucket(List<XenoClientRecord> candidates, int bucket)
        {
            int cumulative = 0;
            foreach (XenoClientRecord candidate in candidates)
            {
                cumulative += EffectiveWeight(candidate);
                if (bucket < cumulative) return candidate;
            }

            return candidates[candidates.Count - 1];
        }

        private static int TotalWeight(List<XenoClientRecord> candidates)
        {
            int total = candidates.Sum(EffectiveWeight);
            return total < 1 ? 1 : total;
        }

        private static int EffectiveWeight(XenoClientRecord candidate)
        {
            return candidate.Weight < 1 ? 1 : candidate.Weight;
        }

        private static XenoClientRecord SelectLeastRecentlyUsed(List<XenoClientRecord> candidates, string routeScopeKey)
        {
            lock (_LeastRecentlyUsedSelections)
            {
                XenoClientRecord selected = candidates[0];
                long selectedSequence = GetLeastRecentlyUsedSequenceUnsafe(routeScopeKey, selected.Id);

                for (int i = 1; i < candidates.Count; i++)
                {
                    XenoClientRecord candidate = candidates[i];
                    long candidateSequence = GetLeastRecentlyUsedSequenceUnsafe(routeScopeKey, candidate.Id);
                    if (candidateSequence < selectedSequence)
                    {
                        selected = candidate;
                        selectedSequence = candidateSequence;
                    }
                }

                MarkLeastRecentlyUsedSelectionUnsafe(routeScopeKey, selected.Id);
                return selected;
            }
        }

        private static long GetLeastRecentlyUsedSequence(string routeScopeKey, string clientId)
        {
            lock (_LeastRecentlyUsedSelections)
            {
                return GetLeastRecentlyUsedSequenceUnsafe(routeScopeKey, clientId);
            }
        }

        private static void MarkLeastRecentlyUsedSelection(string routeScopeKey, string clientId)
        {
            if (String.IsNullOrWhiteSpace(clientId)) return;
            lock (_LeastRecentlyUsedSelections)
            {
                MarkLeastRecentlyUsedSelectionUnsafe(routeScopeKey, clientId);
            }
        }

        private static long GetLeastRecentlyUsedSequenceUnsafe(string routeScopeKey, string clientId)
        {
            string key = BuildLeastRecentlyUsedKey(routeScopeKey, clientId);
            return _LeastRecentlyUsedSelections.TryGetValue(key, out long sequence) ? sequence : 0;
        }

        private static void MarkLeastRecentlyUsedSelectionUnsafe(string routeScopeKey, string clientId)
        {
            string key = BuildLeastRecentlyUsedKey(routeScopeKey, clientId);
            long sequence = Interlocked.Increment(ref _LeastRecentlyUsedSequence);
            _LeastRecentlyUsedSelections[key] = sequence;
        }

        private static string BuildLeastRecentlyUsedKey(string routeScopeKey, string clientId)
        {
            return routeScopeKey + "|" + (clientId ?? String.Empty);
        }

        private XenoClientRecord SelectAdaptive(List<XenoClientRecord> candidates, RoutingDecision decision, AdaptiveLoadBalancingSettings adaptive)
        {
            AdaptiveLoadBalancingSettings settings = adaptive ?? new AdaptiveLoadBalancingSettings();
            int sampleCount = Math.Min(Math.Max(1, settings.SampleCount), candidates.Count);
            List<XenoClientRecord> sampled = candidates
                .OrderBy(item => item.InFlightRequests)
                .ThenByDescending(item => item.LastHeartbeatUtc)
                .ThenBy(item => item.Name)
                .Take(sampleCount)
                .ToList();

            XenoClientRecord selected = sampled[0];
            double selectedScore = BuildAdaptiveScore(selected, settings);
            for (int i = 1; i < sampled.Count; i++)
            {
                double candidateScore = BuildAdaptiveScore(sampled[i], settings);
                if (candidateScore > selectedScore)
                {
                    selected = sampled[i];
                    selectedScore = candidateScore;
                }
            }

            decision.AdaptiveSampleCount = sampleCount;
            decision.AdaptiveScore = selectedScore.ToString("F2", CultureInfo.InvariantCulture);
            return selected;
        }

        private static double BuildAdaptiveScore(XenoClientRecord client, AdaptiveLoadBalancingSettings adaptive)
        {
            AdaptiveLoadBalancingSettings settings = adaptive ?? new AdaptiveLoadBalancingSettings();
            double capacityScore = CapacityScore(client);
            double inFlightScore = InFlightScore(client);
            double clientWeightScore = ClientWeightScore(client);
            double heartbeatScore = HeartbeatScore(client);
            double totalWeight = Math.Max(1, settings.CapacityWeight + settings.InFlightWeight + settings.ClientWeight + settings.HeartbeatWeight);
            double score =
                (capacityScore * settings.CapacityWeight
                + inFlightScore * settings.InFlightWeight
                + clientWeightScore * settings.ClientWeight
                + heartbeatScore * settings.HeartbeatWeight) / totalWeight;
            return ClampScore(score);
        }

        private static double CapacityScore(XenoClientRecord client)
        {
            if (client.MaxParallelRequests <= 0) return 100;
            double remaining = Math.Max(0, client.MaxParallelRequests - client.InFlightRequests);
            return ClampScore((remaining / Math.Max(1, client.MaxParallelRequests)) * 100);
        }

        private static double InFlightScore(XenoClientRecord client)
        {
            int capacity = client.MaxParallelRequests > 0 ? client.MaxParallelRequests : 10;
            return ClampScore(100 - ((double)Math.Max(0, client.InFlightRequests) / Math.Max(1, capacity) * 100));
        }

        private static double ClientWeightScore(XenoClientRecord client)
        {
            return ClampScore(((double)EffectiveWeight(client) / 1000) * 100);
        }

        private static double HeartbeatScore(XenoClientRecord client)
        {
            double ageMs = Math.Max(0, (DateTime.UtcNow - client.LastHeartbeatUtc).TotalMilliseconds);
            return ClampScore(100 - ((ageMs / 60000) * 100));
        }

        private static double ClampScore(double value)
        {
            if (Double.IsNaN(value) || Double.IsInfinity(value)) return 0;
            if (value < 0) return 0;
            if (value > 100) return 100;
            return value;
        }

        private static string BuildRouteScopeKey(RequestContext context, string effectiveModel, bool isEmbedding, RoutingPolicy policy)
        {
            return (context.TenantId ?? String.Empty) + ":" + effectiveModel + ":" + (isEmbedding ? "embeddings" : "completions") + ":" + policy.Id;
        }

        /// <summary>
        /// Load the tenant's routing policies through the sliding-expiration cache. Routing policies change
        /// rarely, so serving a copy up to the cache TTL old is acceptable per design; the cache is keyed by
        /// tenant and holds the full policy list for that tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The tenant's routing policies.</returns>
        private async Task<List<RoutingPolicy>> LoadTenantRoutingPoliciesAsync(string tenantId, CancellationToken token)
        {
            if (String.IsNullOrEmpty(tenantId) || !_CacheEnabled)
            {
                return await _Database.RoutingPolicies!.EnumerateAsync(tenantId, token).ConfigureAwait(false);
            }

            List<RoutingPolicy>? cached = await _RoutingPolicyCache.GetOrLoadAsync(
                tenantId,
                async loaderToken => await _Database.RoutingPolicies!.EnumerateAsync(tenantId, loaderToken).ConfigureAwait(false),
                token).ConfigureAwait(false);
            return cached ?? new List<RoutingPolicy>();
        }

        private async Task<RoutingPolicy> ResolveRoutingPolicyAsync(string tenantId, string effectiveModel, List<XenoClientRecord> candidates, CancellationToken token)
        {
            List<RoutingPolicy> tenantPolicies = await LoadTenantRoutingPoliciesAsync(tenantId, token).ConfigureAwait(false);
            List<RoutingPolicy> policies = tenantPolicies
                .Where(item => item.TenantId == tenantId)
                .Where(item => item.Active)
                .ToList();

            RoutingPolicy? selected = null;
            int selectedScore = -1;
            DateTime selectedUpdated = DateTime.MinValue;

            foreach (RoutingPolicy policy in policies)
            {
                int modelScore = ModelSpecificity(policy.ModelName, effectiveModel);
                if (modelScore < 0) continue;

                int clientScore = ClientSpecificity(policy.ClientName, candidates);
                if (clientScore < 0) continue;

                int score = (modelScore * 10) + clientScore;
                bool replace = selected == null || score > selectedScore || (score == selectedScore && policy.LastUpdateUtc > selectedUpdated);
                if (replace)
                {
                    selected = policy;
                    selectedScore = score;
                    selectedUpdated = policy.LastUpdateUtc;
                }
            }

            return selected ?? FallbackPolicy(tenantId, tenantPolicies);
        }

        private static void ApplyRoutingPolicy(RoutingDecision decision, RoutingPolicy policy)
        {
            decision.RoutingPolicyId = policy.Id;
            decision.RoutingPolicyName = policy.Name;
            decision.RoutingPolicyModelName = policy.ModelName;
            decision.RoutingPolicyClientName = policy.ClientName;
            decision.RoutingPolicyRequestTimeoutMs = policy.RequestTimeoutMs;
            decision.RoutingPolicyReason = BuildPolicyReason(policy);
        }

        private static IEnumerable<XenoClientRecord> FilterCandidatesByPolicy(List<XenoClientRecord> candidates, RoutingPolicy policy)
        {
            if (String.Equals(policy.ClientName, "*", StringComparison.Ordinal)) return candidates;
            return candidates.Where(item => String.Equals(item.Name, policy.ClientName, StringComparison.OrdinalIgnoreCase));
        }

        private static bool MatchesPolicyValue(string? policyValue, string? actual)
        {
            if (String.IsNullOrWhiteSpace(policyValue)) return true;
            if (String.Equals(policyValue, "*", StringComparison.Ordinal)) return true;
            return String.Equals(policyValue, actual, StringComparison.OrdinalIgnoreCase);
        }

        private static int ModelSpecificity(string? policyModelName, string effectiveModel)
        {
            if (String.Equals(policyModelName, "*", StringComparison.Ordinal) || String.IsNullOrWhiteSpace(policyModelName)) return 0;
            return MatchesPolicyValue(policyModelName, effectiveModel) ? 1 : -1;
        }

        private static int ClientSpecificity(string? policyClientName, List<XenoClientRecord> candidates)
        {
            if (String.Equals(policyClientName, "*", StringComparison.Ordinal) || String.IsNullOrWhiteSpace(policyClientName)) return 0;
            bool clientExists = candidates.Any(item => MatchesPolicyValue(policyClientName, item.Name));
            return clientExists ? 1 : -1;
        }

        private static string BuildPolicyReason(RoutingPolicy policy)
        {
            return "Matched routing policy '" + policy.Name + "' for model '" + policy.ModelName + "' and client '" + policy.ClientName + "'.";
        }

        private RoutingPolicy FallbackPolicy(string tenantId, List<RoutingPolicy> tenantPolicies)
        {
            RoutingPolicy? defaultPolicy = tenantPolicies.FirstOrDefault(item =>
                item.TenantId == tenantId &&
                item.IsDefault);
            if (defaultPolicy != null) return defaultPolicy;

            return new RoutingPolicy
            {
                TenantId = tenantId,
                Name = "Default Routing Policy",
                ModelName = "*",
                ClientName = "*",
                RequestTimeoutMs = _Settings.DefaultTimeoutMs,
                LoadBalancingMode = LoadBalancingModeEnum.LeastRecentlyUsed,
                Adaptive = _Settings.Adaptive,
                IsDefault = true,
                Active = true
            };
        }

        private static string BuildSelectionReason(LoadBalancingModeEnum mode, XenoClientRecord selected, RoutingDecision decision)
        {
            string basis = "Selected client '" + selected.Name + "' using " + mode + " load balancing.";
            if (mode == LoadBalancingModeEnum.Adaptive && !String.IsNullOrWhiteSpace(decision.AdaptiveScore))
            {
                basis += " Adaptive score " + decision.AdaptiveScore + " from " + decision.AdaptiveSampleCount + " sampled candidate(s).";
            }

            return basis;
        }
    }
}

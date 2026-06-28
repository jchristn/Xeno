namespace Xeno.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Enums;
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

        private readonly XenoPlatform _Platform;
        private readonly RoutingSettings _Settings;

        /// <summary>
        /// Instantiate the routing service.
        /// </summary>
        /// <param name="platform">Platform state.</param>
        /// <param name="settings">Routing settings.</param>
        public RoutingService(XenoPlatform platform, RoutingSettings? settings = null)
        {
            _Platform = platform ?? throw new ArgumentNullException(nameof(platform));
            _Settings = settings ?? new RoutingSettings();
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
            if (context == null) throw new ArgumentNullException(nameof(context));
            token.ThrowIfCancellationRequested();

            RoutingDecision decision = new RoutingDecision
            {
                Success = false,
                OutcomeCode = "Started",
                Message = "Routing started."
            };

            string effectiveModel = ResolveModel(context.TenantId, model, isEmbedding);
            decision.Timeline.Add("ModelResolution");
            if (String.IsNullOrWhiteSpace(effectiveModel))
            {
                decision.HttpStatusCode = 404;
                decision.OutcomeCode = "ModelNotFound";
                decision.Message = "No advertised model can service the request.";
                return Task.FromResult(decision);
            }
            decision.EffectiveModel = effectiveModel;

            List<XenoClientRecord> candidates = _Platform.Clients
                .Where(item => item.TenantId == context.TenantId)
                .Where(item => item.Active)
                .Where(item => item.ServiceState == ClientServiceStateEnum.Normal)
                .Where(item => item.Connected)
                .Where(item => item.LastHeartbeatUtc >= DateTime.UtcNow.AddSeconds(-60))
                .Where(item => item.Models.Any(model => String.Equals(model.Name, effectiveModel, StringComparison.OrdinalIgnoreCase) && (isEmbedding ? model.SupportsEmbeddings : model.SupportsCompletions)))
                .Where(item => item.MaxParallelRequests == 0 || item.InFlightRequests < item.MaxParallelRequests)
                .OrderBy(item => item.Name)
                .ThenByDescending(item => item.Weight)
                .ThenBy(item => item.Id)
                .ToList();
            decision.Timeline.Add("ClientInventory");

            RoutingPolicy policy = ResolveRoutingPolicy(context.TenantId, effectiveModel, candidates);
            ApplyRoutingPolicy(decision, policy);
            decision.Timeline.Add("PolicyResolution");

            List<XenoClientRecord> policyCandidates = FilterCandidatesByPolicy(candidates, policy).ToList();
            candidates = policyCandidates;

            if (candidates.Count < 1)
            {
                decision.HttpStatusCode = 502;
                decision.OutcomeCode = "NoEligibleClients";
                decision.Message = "No connected client can service the request.";
                return Task.FromResult(decision);
            }

            XenoClientRecord selected = SelectClient(candidates, policy.LoadBalancingMode, BuildRouteScopeKey(context, effectiveModel, isEmbedding, policy), decision, policy.Adaptive);
            XenoClientModel? selectedModel = selected.Models.FirstOrDefault(item => String.Equals(item.Name, effectiveModel, StringComparison.OrdinalIgnoreCase));
            decision.CandidateClientIds = candidates.Select(item => item.Id).ToList();
            decision.CandidateCount = candidates.Count;
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
            return Task.FromResult(decision);
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

            RequestHistoryEntry entry = new RequestHistoryEntry
            {
                TenantId = context.TenantId,
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
                AuthScheme = context.AuthScheme,
                RoutingDecision = decision
            };
            _Platform.RequestHistory.Add(entry);
            return entry;
        }

        private string ResolveModel(string tenantId, string? requestedModel, bool isEmbedding)
        {
            List<ModelDefinition> definitions = _Platform.ModelDefinitions
                .Where(item => item.TenantId == tenantId)
                .Where(item => isEmbedding ? item.SupportsEmbeddings : item.SupportsCompletions)
                .ToList();

            if (!String.IsNullOrWhiteSpace(requestedModel))
            {
                ModelDefinition? requested = definitions.FirstOrDefault(item => String.Equals(item.Name, requestedModel, StringComparison.OrdinalIgnoreCase));
                if (requested != null) return requested.Name;

                bool clientAdvertisesRequestedModel = _Platform.Clients
                    .Where(item => item.TenantId == tenantId)
                    .Any(item => item.Models.Any(model => String.Equals(model.Name, requestedModel, StringComparison.OrdinalIgnoreCase)));
                return clientAdvertisesRequestedModel ? requestedModel : String.Empty;
            }

            string? definedModel = definitions.FirstOrDefault()?.Name;
            if (!String.IsNullOrWhiteSpace(definedModel)) return definedModel;

            return _Platform.Clients
                .Where(item => item.TenantId == tenantId)
                .SelectMany(item => item.Models)
                .Select(item => item.Name)
                .FirstOrDefault() ?? String.Empty;
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
            XenoClientRecord selected = candidates[0];
            long selectedSequence = GetLeastRecentlyUsedSequence(routeScopeKey, selected.Id);

            for (int i = 1; i < candidates.Count; i++)
            {
                XenoClientRecord candidate = candidates[i];
                long candidateSequence = GetLeastRecentlyUsedSequence(routeScopeKey, candidate.Id);
                if (candidateSequence < selectedSequence)
                {
                    selected = candidate;
                    selectedSequence = candidateSequence;
                }
            }

            MarkLeastRecentlyUsedSelection(routeScopeKey, selected.Id);
            return selected;
        }

        private static long GetLeastRecentlyUsedSequence(string routeScopeKey, string clientId)
        {
            string key = BuildLeastRecentlyUsedKey(routeScopeKey, clientId);
            lock (_LeastRecentlyUsedSelections)
            {
                return _LeastRecentlyUsedSelections.TryGetValue(key, out long sequence) ? sequence : 0;
            }
        }

        private static void MarkLeastRecentlyUsedSelection(string routeScopeKey, string clientId)
        {
            if (String.IsNullOrWhiteSpace(clientId)) return;
            string key = BuildLeastRecentlyUsedKey(routeScopeKey, clientId);
            long sequence = Interlocked.Increment(ref _LeastRecentlyUsedSequence);
            lock (_LeastRecentlyUsedSelections)
            {
                _LeastRecentlyUsedSelections[key] = sequence;
            }
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

        private RoutingPolicy ResolveRoutingPolicy(string tenantId, string effectiveModel, List<XenoClientRecord> candidates)
        {
            List<RoutingPolicy> policies = _Platform.RoutingPolicies
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

            return selected ?? FallbackPolicy(tenantId);
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

        private RoutingPolicy FallbackPolicy(string tenantId)
        {
            RoutingPolicy? defaultPolicy = _Platform.RoutingPolicies.FirstOrDefault(item =>
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

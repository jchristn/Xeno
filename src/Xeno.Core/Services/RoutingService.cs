namespace Xeno.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Enums;
    using Xeno.Core.Models;

    /// <summary>
    /// Conductor-compatible routing service adapted for connected Xeno clients.
    /// </summary>
    public class RoutingService
    {
        private readonly XenoPlatform _Platform;
        private readonly object _Lock = new object();
        private int _RoundRobinIndex;

        /// <summary>
        /// Instantiate the routing service.
        /// </summary>
        /// <param name="platform">Platform state.</param>
        public RoutingService(XenoPlatform platform)
        {
            _Platform = platform ?? throw new ArgumentNullException(nameof(platform));
        }

        /// <summary>
        /// Route a request to an eligible endpoint.
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
            decision.Timeline.Add("VmrResolved");

            VirtualModelRunner? vmr = _Platform.VirtualModelRunners.FirstOrDefault(item => item.TenantId == context.TenantId && item.Active);
            if (vmr == null)
            {
                decision.HttpStatusCode = 404;
                decision.OutcomeCode = "VirtualModelRunnerNotFound";
                decision.Message = "No active virtual model runner exists for the tenant.";
                return Task.FromResult(decision);
            }

            if (isEmbedding && !vmr.AllowEmbeddings)
            {
                decision.HttpStatusCode = 403;
                decision.OutcomeCode = "EmbeddingsNotAllowed";
                decision.Message = "Embeddings are not allowed.";
                return Task.FromResult(decision);
            }

            if (!isEmbedding && !vmr.AllowCompletions)
            {
                decision.HttpStatusCode = 403;
                decision.OutcomeCode = "CompletionsNotAllowed";
                decision.Message = "Completions are not allowed.";
                return Task.FromResult(decision);
            }
            decision.Timeline.Add("RequestTypeGate");

            string effectiveModel = ResolveModel(vmr, model, isEmbedding);
            if (String.IsNullOrWhiteSpace(effectiveModel))
            {
                decision.HttpStatusCode = 401;
                decision.OutcomeCode = "ModelNotAttached";
                decision.Message = "The requested model is not attached to the virtual model runner.";
                return Task.FromResult(decision);
            }
            decision.EffectiveModel = effectiveModel;
            decision.Timeline.Add("ModelResolution");

            ModelAccessPolicy? modelAccessPolicy = ResolveModelAccessPolicy(vmr);
            if (!EvaluateModelAccess(modelAccessPolicy, effectiveModel, decision))
            {
                return Task.FromResult(decision);
            }
            decision.Timeline.Add("ModelAccess");

            if (!EvaluateReservations(context, vmr, decision))
            {
                return Task.FromResult(decision);
            }
            decision.Timeline.Add("Reservations");

            List<ModelRunnerEndpoint> candidates = _Platform.Endpoints
                .Where(item => item.TenantId == context.TenantId)
                .Where(item => vmr.ModelRunnerEndpointIds.Contains(item.Id))
                .Where(item => item.Active)
                .Where(item => item.ServiceState == EndpointServiceStateEnum.Normal)
                .Where(item => _Platform.Clients.Any(client => client.TenantId == context.TenantId && client.Id == item.ClientId && client.Connected))
                .Where(item => item.Models.Contains(effectiveModel, StringComparer.OrdinalIgnoreCase))
                .Where(item => item.MaxParallelRequests == 0 || item.InFlightRequests < item.MaxParallelRequests)
                .ToList();

            if (candidates.Count < 1)
            {
                decision.HttpStatusCode = 502;
                decision.OutcomeCode = "NoEligibleEndpoints";
                decision.Message = "No connected endpoint can service the request.";
                return Task.FromResult(decision);
            }
            decision.CandidateEndpointIds = candidates.Select(item => item.Id).ToList();
            decision.Timeline.Add("EndpointInventory");

            LoadBalancingPolicy? loadBalancingPolicy = ResolveLoadBalancingPolicy(vmr);
            ModelRunnerEndpoint selected = Select(candidates, vmr.LoadBalancingMode, loadBalancingPolicy);
            decision.Success = true;
            decision.HttpStatusCode = 200;
            decision.OutcomeCode = "Routed";
            decision.Message = "Request routed to endpoint '" + selected.Name + "'.";
            decision.SelectedEndpointId = selected.Id;
            decision.SelectedClientId = selected.ClientId;
            decision.Timeline.Add("EndpointSelection");
            return Task.FromResult(decision);
        }

        /// <summary>
        /// Record a completed request.
        /// </summary>
        /// <param name="context">Request context.</param>
        /// <param name="path">Request path.</param>
        /// <param name="decision">Routing decision.</param>
        /// <returns>Request history entry.</returns>
        public RequestHistoryEntry RecordHistory(RequestContext context, string path, RoutingDecision decision)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (decision == null) throw new ArgumentNullException(nameof(decision));

            RequestHistoryEntry entry = new RequestHistoryEntry
            {
                TenantId = context.TenantId,
                Path = path,
                StatusCode = decision.HttpStatusCode,
                RoutingDecision = decision
            };
            _Platform.RequestHistory.Add(entry);
            return entry;
        }

        private string ResolveModel(VirtualModelRunner vmr, string? requestedModel, bool isEmbedding)
        {
            List<ModelDefinition> definitions = _Platform.ModelDefinitions
                .Where(item => item.TenantId == vmr.TenantId)
                .Where(item => vmr.ModelDefinitionIds.Contains(item.Id))
                .Where(item => isEmbedding ? item.SupportsEmbeddings : item.SupportsCompletions)
                .ToList();

            if (!String.IsNullOrWhiteSpace(requestedModel))
            {
                ModelDefinition? requested = definitions.FirstOrDefault(item => String.Equals(item.Name, requestedModel, StringComparison.OrdinalIgnoreCase));
                if (requested != null) return requested.Name;
                if (vmr.StrictMode) return String.Empty;
            }

            return definitions.FirstOrDefault()?.Name ?? String.Empty;
        }

        private ModelAccessPolicy? ResolveModelAccessPolicy(VirtualModelRunner vmr)
        {
            if (!String.IsNullOrWhiteSpace(vmr.ModelAccessPolicyId))
            {
                ModelAccessPolicy? byId = _Platform.ModelAccessPolicies.FirstOrDefault(item => item.Id == vmr.ModelAccessPolicyId && item.TenantId == vmr.TenantId && item.Active);
                if (byId != null) return byId;
            }

            return _Platform.ModelAccessPolicies.FirstOrDefault(item => item.TenantId == vmr.TenantId && item.Active);
        }

        private LoadBalancingPolicy? ResolveLoadBalancingPolicy(VirtualModelRunner vmr)
        {
            if (!String.IsNullOrWhiteSpace(vmr.LoadBalancingPolicyId))
            {
                LoadBalancingPolicy? byId = _Platform.LoadBalancingPolicies.FirstOrDefault(item => item.Id == vmr.LoadBalancingPolicyId && item.TenantId == vmr.TenantId && item.Active);
                if (byId != null) return byId;
            }

            return _Platform.LoadBalancingPolicies.FirstOrDefault(item => item.TenantId == vmr.TenantId && item.Active);
        }

        private bool EvaluateModelAccess(ModelAccessPolicy? policy, string effectiveModel, RoutingDecision decision)
        {
            if (policy == null)
            {
                decision.ModelAccessDecision = ModelAccessDecisionEnum.Permit;
                return true;
            }

            bool denied = policy.DeniedModels.Contains(effectiveModel, StringComparer.OrdinalIgnoreCase);
            bool allowListDenied = policy.AllowedModels.Count > 0 && !policy.AllowedModels.Contains(effectiveModel, StringComparer.OrdinalIgnoreCase);
            if (denied || allowListDenied || policy.DefaultDecision == ModelAccessDecisionEnum.Deny)
            {
                decision.ModelAccessDecision = ModelAccessDecisionEnum.Deny;
                if (policy.Mode == ModelAccessModeEnum.Enforce)
                {
                    decision.HttpStatusCode = 403;
                    decision.OutcomeCode = "ModelAccessDenied";
                    decision.Message = "Model access policy denied the request.";
                    return false;
                }

                decision.Timeline.Add("ModelAccessMonitorDeny");
            }
            else
            {
                decision.ModelAccessDecision = ModelAccessDecisionEnum.Permit;
            }

            return true;
        }

        private bool EvaluateReservations(RequestContext context, VirtualModelRunner vmr, RoutingDecision decision)
        {
            DateTime now = DateTime.UtcNow;
            List<VirtualModelRunnerReservation> activeReservations = _Platform.VirtualModelRunnerReservations
                .Where(item => item.TenantId == context.TenantId)
                .Where(item => item.VirtualModelRunnerId == vmr.Id)
                .Where(item => item.Active)
                .Where(item => item.StartUtc <= now && item.EndUtc >= now)
                .ToList();

            if (activeReservations.Count == 0)
            {
                decision.ReservationDecision = ReservationDecisionEnum.NoReservation;
                return true;
            }

            if (activeReservations.Count > 1)
            {
                decision.ReservationDecision = ReservationDecisionEnum.Conflict;
                decision.ReservationReasonCode = "ReservationConflict";
                decision.HttpStatusCode = 409;
                decision.OutcomeCode = "ReservationConflict";
                decision.Message = "Multiple active reservations conflict.";
                return false;
            }

            VirtualModelRunnerReservation reservation = activeReservations[0];
            if (reservation.SubjectIds.Count == 0 || (!String.IsNullOrWhiteSpace(context.PrincipalId) && reservation.SubjectIds.Contains(context.PrincipalId, StringComparer.OrdinalIgnoreCase)))
            {
                decision.ReservationDecision = ReservationDecisionEnum.Allowed;
                decision.ReservationReasonCode = "ReservationAllowed";
                return true;
            }

            decision.ReservationDecision = ReservationDecisionEnum.Denied;
            decision.ReservationReasonCode = "ReservationSubjectDenied";
            decision.HttpStatusCode = 403;
            decision.OutcomeCode = "ReservationDenied";
            decision.Message = "The active reservation does not include this principal.";
            return false;
        }

        private ModelRunnerEndpoint Select(List<ModelRunnerEndpoint> candidates, LoadBalancingModeEnum mode, LoadBalancingPolicy? policy)
        {
            if (mode == LoadBalancingModeEnum.FirstAvailable) return candidates[0];
            if (mode == LoadBalancingModeEnum.Random) return candidates[Random.Shared.Next(candidates.Count)];

            if (policy != null && policy.Active)
            {
                if (String.Equals(policy.RankingMetric, "weight", StringComparison.OrdinalIgnoreCase))
                {
                    return policy.PreferLower
                        ? candidates.OrderBy(item => item.Weight).ThenBy(item => item.InFlightRequests).First()
                        : candidates.OrderByDescending(item => item.Weight).ThenBy(item => item.InFlightRequests).First();
                }

                if (String.Equals(policy.RankingMetric, "firstAvailable", StringComparison.OrdinalIgnoreCase))
                {
                    return candidates[0];
                }

                return policy.PreferLower
                    ? candidates.OrderBy(item => item.InFlightRequests).ThenByDescending(item => item.Weight).First()
                    : candidates.OrderByDescending(item => item.InFlightRequests).ThenByDescending(item => item.Weight).First();
            }

            lock (_Lock)
            {
                ModelRunnerEndpoint selected = candidates[_RoundRobinIndex % candidates.Count];
                _RoundRobinIndex++;
                return selected;
            }
        }
    }
}

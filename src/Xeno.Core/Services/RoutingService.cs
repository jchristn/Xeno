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
            decision.Timeline.Add("ModelAccess");

            List<ModelRunnerEndpoint> candidates = _Platform.Endpoints
                .Where(item => item.TenantId == context.TenantId)
                .Where(item => vmr.ModelRunnerEndpointIds.Contains(item.Id))
                .Where(item => item.Active)
                .Where(item => item.ServiceState == EndpointServiceStateEnum.Normal)
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
            decision.Timeline.Add("EndpointInventory");

            ModelRunnerEndpoint selected = Select(candidates, vmr.LoadBalancingMode);
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

        private ModelRunnerEndpoint Select(List<ModelRunnerEndpoint> candidates, LoadBalancingModeEnum mode)
        {
            if (mode == LoadBalancingModeEnum.FirstAvailable) return candidates[0];
            if (mode == LoadBalancingModeEnum.Random) return candidates[Random.Shared.Next(candidates.Count)];

            lock (_Lock)
            {
                ModelRunnerEndpoint selected = candidates[_RoundRobinIndex % candidates.Count];
                _RoundRobinIndex++;
                return selected;
            }
        }
    }
}

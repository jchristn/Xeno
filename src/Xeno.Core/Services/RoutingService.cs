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
    /// Routes inference requests directly to connected Xeno model runners.
    /// </summary>
    public class RoutingService
    {
        private readonly XenoPlatform _Platform;

        /// <summary>
        /// Instantiate the routing service.
        /// </summary>
        /// <param name="platform">Platform state.</param>
        public RoutingService(XenoPlatform platform)
        {
            _Platform = platform ?? throw new ArgumentNullException(nameof(platform));
        }

        /// <summary>
        /// Route a request to an eligible model runner endpoint.
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

            List<ModelRunnerEndpoint> candidates = _Platform.Endpoints
                .Where(item => item.TenantId == context.TenantId)
                .Where(item => item.Active)
                .Where(item => item.ServiceState == EndpointServiceStateEnum.Normal)
                .Where(item => item.Models.Contains(effectiveModel, StringComparer.OrdinalIgnoreCase))
                .Where(item => item.MaxParallelRequests == 0 || item.InFlightRequests < item.MaxParallelRequests)
                .Where(item => _Platform.Clients.Any(client => IsClientEligible(context, item, client)))
                .OrderBy(item => item.InFlightRequests)
                .ThenByDescending(item => item.Weight)
                .ThenBy(item => item.Name)
                .ToList();
            decision.Timeline.Add("EndpointInventory");

            if (candidates.Count < 1)
            {
                decision.HttpStatusCode = 502;
                decision.OutcomeCode = "NoEligibleEndpoints";
                decision.Message = "No connected model runner can service the request.";
                return Task.FromResult(decision);
            }

            ModelRunnerEndpoint selected = candidates[0];
            decision.CandidateEndpointIds = candidates.Select(item => item.Id).ToList();
            decision.Success = true;
            decision.HttpStatusCode = 200;
            decision.OutcomeCode = "Routed";
            decision.Message = "Request routed to model runner endpoint '" + selected.Name + "'.";
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

                bool endpointAdvertisesRequestedModel = _Platform.Endpoints
                    .Where(item => item.TenantId == tenantId)
                    .Any(item => item.Models.Contains(requestedModel, StringComparer.OrdinalIgnoreCase));
                return endpointAdvertisesRequestedModel ? requestedModel : String.Empty;
            }

            string? definedModel = definitions.FirstOrDefault()?.Name;
            if (!String.IsNullOrWhiteSpace(definedModel)) return definedModel;

            return _Platform.Endpoints
                .Where(item => item.TenantId == tenantId)
                .SelectMany(item => item.Models)
                .FirstOrDefault() ?? String.Empty;
        }

        private static bool IsClientEligible(RequestContext context, ModelRunnerEndpoint endpoint, XenoClientRecord client)
        {
            return client.TenantId == context.TenantId &&
                   client.Id == endpoint.ClientId &&
                   client.Connected &&
                   client.LastHeartbeatUtc >= DateTime.UtcNow.AddSeconds(-60);
        }
    }
}

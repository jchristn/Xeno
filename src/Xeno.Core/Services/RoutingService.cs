namespace Xeno.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Models;

    /// <summary>
    /// Routes inference requests directly to connected Xeno clients.
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
                .Where(item => item.ServiceState == Xeno.Core.Enums.ClientServiceStateEnum.Normal)
                .Where(item => item.Connected)
                .Where(item => item.LastHeartbeatUtc >= DateTime.UtcNow.AddSeconds(-60))
                .Where(item => item.Models.Any(model => String.Equals(model.Name, effectiveModel, StringComparison.OrdinalIgnoreCase) && (isEmbedding ? model.SupportsEmbeddings : model.SupportsCompletions)))
                .Where(item => item.MaxParallelRequests == 0 || item.InFlightRequests < item.MaxParallelRequests)
                .OrderBy(item => item.InFlightRequests)
                .ThenByDescending(item => item.Weight)
                .ThenBy(item => item.Name)
                .ToList();
            decision.Timeline.Add("ClientInventory");

            if (candidates.Count < 1)
            {
                decision.HttpStatusCode = 502;
                decision.OutcomeCode = "NoEligibleClients";
                decision.Message = "No connected client can service the request.";
                return Task.FromResult(decision);
            }

            XenoClientRecord selected = candidates[0];
            decision.CandidateClientIds = candidates.Select(item => item.Id).ToList();
            decision.Success = true;
            decision.HttpStatusCode = 200;
            decision.OutcomeCode = "Routed";
            decision.Message = "Request routed to client '" + selected.Name + "'.";
            decision.SelectedClientId = selected.Id;
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
    }
}

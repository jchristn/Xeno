namespace Xeno.Core.Models
{
    using System;
    using System.Collections.Generic;
    using Xeno.Core.Enums;
    using Xeno.Core.Helpers;

    /// <summary>
    /// Routing decision.
    /// </summary>
    public class RoutingDecision
    {
        /// <summary>
        /// Whether routing succeeded.
        /// </summary>
        public bool Success { get; set; } = false;

        /// <summary>
        /// Outcome code.
        /// </summary>
        public string OutcomeCode { get; set; } = "Unknown";

        /// <summary>
        /// Human-readable message.
        /// </summary>
        public string Message { get; set; } = String.Empty;

        /// <summary>
        /// HTTP status code.
        /// </summary>
        public int HttpStatusCode { get; set; } = 200;

        /// <summary>
        /// Selected client ID.
        /// </summary>
        public string? SelectedClientId { get; set; } = null;

        /// <summary>
        /// Selected client name.
        /// </summary>
        public string? SelectedClientName { get; set; } = null;

        /// <summary>
        /// Selected client provider.
        /// </summary>
        public string? SelectedProvider { get; set; } = null;

        /// <summary>
        /// Selected client-local source.
        /// </summary>
        public string? SelectedSource { get; set; } = null;

        /// <summary>
        /// Selected client in-flight request count at selection time.
        /// </summary>
        public int? SelectedClientInFlightRequests { get; set; } = null;

        /// <summary>
        /// Selected client maximum parallel request count.
        /// </summary>
        public int? SelectedClientMaxParallelRequests { get; set; } = null;

        /// <summary>
        /// Selected client routing weight.
        /// </summary>
        public int? SelectedClientWeight { get; set; } = null;

        /// <summary>
        /// Selected client last heartbeat timestamp.
        /// </summary>
        public DateTime? SelectedClientLastHeartbeatUtc { get; set; } = null;

        /// <summary>
        /// Effective model name.
        /// </summary>
        public string? EffectiveModel { get; set; } = null;

        /// <summary>
        /// Candidate client IDs.
        /// </summary>
        public List<string> CandidateClientIds { get; set; } = new List<string>();

        /// <summary>
        /// Number of eligible candidate clients.
        /// </summary>
        public int CandidateCount { get; set; } = 0;

        /// <summary>
        /// Human-readable selection reason.
        /// </summary>
        public string SelectionReason { get; set; } = String.Empty;

        /// <summary>
        /// Load-balancing mode used for selection.
        /// </summary>
        public LoadBalancingModeEnum LoadBalancingMode { get; set; } = LoadBalancingModeEnum.RoundRobin;

        /// <summary>
        /// Adaptive sample count when adaptive routing is used.
        /// </summary>
        public int? AdaptiveSampleCount { get; set; } = null;

        /// <summary>
        /// Adaptive selected score when adaptive routing is used.
        /// </summary>
        public string? AdaptiveScore { get; set; } = null;

        /// <summary>
        /// Server-to-client round-trip duration in milliseconds.
        /// </summary>
        public long? ClientRoundTripMs { get; set; } = null;

        /// <summary>
        /// Timeline entries.
        /// </summary>
        public List<string> Timeline { get; set; } = new List<string>();
    }
}

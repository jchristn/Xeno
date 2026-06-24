namespace Xeno.Core.Models
{
    using System;
    using System.Collections.Generic;
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
        /// Effective model name.
        /// </summary>
        public string? EffectiveModel { get; set; } = null;

        /// <summary>
        /// Candidate client IDs.
        /// </summary>
        public List<string> CandidateClientIds { get; set; } = new List<string>();

        /// <summary>
        /// Timeline entries.
        /// </summary>
        public List<string> Timeline { get; set; } = new List<string>();
    }
}

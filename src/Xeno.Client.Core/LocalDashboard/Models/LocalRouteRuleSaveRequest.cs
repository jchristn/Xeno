namespace Xeno.Client.LocalDashboard.Models
{
    using System;

    /// <summary>
    /// Create or replace a local inference route rule.
    /// </summary>
    public class LocalRouteRuleSaveRequest
    {
        /// <summary>
        /// Model. Required.
        /// </summary>
        public string Model { get; set; } = String.Empty;

        /// <summary>
        /// "Local" or "Network". Default "Local".
        /// </summary>
        public LocalInferenceRouteTargetEnum Target { get; set; } = LocalInferenceRouteTargetEnum.Local;

        /// <summary>
        /// Provider for local targets.
        /// </summary>
        public string? Provider { get; set; } = null;

        /// <summary>
        /// Whether the model may fall back to the network. Default false.
        /// </summary>
        public bool FallbackToNetwork { get; set; } = false;

        /// <summary>
        /// Model of the rule being edited, when renaming. Null for a new rule.
        /// </summary>
        public string? OriginalModel { get; set; } = null;
    }
}

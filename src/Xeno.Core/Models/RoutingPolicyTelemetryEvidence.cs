namespace Xeno.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Telemetry evidence captured during routing policy evaluation.
    /// </summary>
    public class RoutingPolicyTelemetryEvidence
    {
        /// <summary>
        /// Client identifier.
        /// </summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>
        /// Whether telemetry selected this client.
        /// </summary>
        public bool Selected { get; set; } = false;

        /// <summary>
        /// Whether telemetry rejected this client.
        /// </summary>
        public bool Rejected { get; set; } = false;

        /// <summary>
        /// Human-readable diagnostic reason.
        /// </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// Resolved metric values.
        /// </summary>
        public List<RoutingPolicyMetricValue> Metrics { get; set; } = new List<RoutingPolicyMetricValue>();
    }
}

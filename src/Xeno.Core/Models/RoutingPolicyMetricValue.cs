namespace Xeno.Core.Models
{
    /// <summary>
    /// Resolved telemetry metric value used during routing.
    /// </summary>
    public class RoutingPolicyMetricValue
    {
        /// <summary>
        /// Metric name.
        /// </summary>
        public string Metric { get; set; } = string.Empty;

        /// <summary>
        /// Numeric value.
        /// </summary>
        public double? NumberValue { get; set; } = null;

        /// <summary>
        /// Boolean value.
        /// </summary>
        public bool? BooleanValue { get; set; } = null;

        /// <summary>
        /// String value.
        /// </summary>
        public string? StringValue { get; set; } = null;

        /// <summary>
        /// Whether the metric was available.
        /// </summary>
        public bool Available { get; set; } = false;
    }
}

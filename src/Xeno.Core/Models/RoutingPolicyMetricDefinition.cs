namespace Xeno.Core.Models
{
    /// <summary>
    /// Telemetry metric definition available to routing policies.
    /// </summary>
    public class RoutingPolicyMetricDefinition
    {
        /// <summary>
        /// Metric name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Metric value type.
        /// </summary>
        public string ValueType { get; set; } = "Number";
    }
}

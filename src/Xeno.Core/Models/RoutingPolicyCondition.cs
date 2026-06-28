namespace Xeno.Core.Models
{
    using Xeno.Core.Enums;

    /// <summary>
    /// Telemetry filter condition for a routing policy.
    /// </summary>
    public class RoutingPolicyCondition
    {
        /// <summary>
        /// Metric name.
        /// </summary>
        public string Metric { get; set; } = string.Empty;

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public RoutingPolicyOperatorEnum Operator { get; set; } = RoutingPolicyOperatorEnum.Equal;

        /// <summary>
        /// Metric value type.
        /// </summary>
        public RoutingPolicyMetricValueTypeEnum ValueType { get; set; } = RoutingPolicyMetricValueTypeEnum.Number;

        /// <summary>
        /// Comparison value encoded as a string.
        /// </summary>
        public string Value { get; set; } = string.Empty;
    }
}

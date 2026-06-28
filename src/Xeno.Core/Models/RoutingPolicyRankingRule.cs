namespace Xeno.Core.Models
{
    using Xeno.Core.Enums;

    /// <summary>
    /// Telemetry ranking rule for a routing policy.
    /// </summary>
    public class RoutingPolicyRankingRule
    {
        /// <summary>
        /// Metric name.
        /// </summary>
        public string Metric { get; set; } = string.Empty;

        /// <summary>
        /// Ranking direction.
        /// </summary>
        public RoutingPolicyRankingDirectionEnum Direction { get; set; } = RoutingPolicyRankingDirectionEnum.Ascending;

        /// <summary>
        /// Ranking weight. Must be positive.
        /// </summary>
        public double Weight { get; set; } = 1;
    }
}

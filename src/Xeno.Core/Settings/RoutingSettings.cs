namespace Xeno.Core.Settings
{
    using Xeno.Core.Enums;

    /// <summary>
    /// Routing settings.
    /// </summary>
    public class RoutingSettings
    {
        /// <summary>
        /// Default timeout in milliseconds.
        /// </summary>
        public int DefaultTimeoutMs { get; set; } = 300000;

        /// <summary>
        /// Connected-client load-balancing mode.
        /// </summary>
        public LoadBalancingModeEnum LoadBalancingMode { get; set; } = LoadBalancingModeEnum.RoundRobin;

        /// <summary>
        /// Adaptive selection settings.
        /// </summary>
        public AdaptiveLoadBalancingSettings Adaptive { get; set; } = new AdaptiveLoadBalancingSettings();
    }
}

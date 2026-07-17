namespace Xeno.Core.Settings
{
    using Xeno.Core.Enums;

    /// <summary>
    /// Routing settings.
    /// </summary>
    public class RoutingSettings
    {
        private int _ClientFreshnessTimeoutMs = 60000;

        /// <summary>
        /// Default timeout in milliseconds.
        /// </summary>
        public int DefaultTimeoutMs { get; set; } = 300000;

        /// <summary>
        /// Connected-client load-balancing mode.
        /// </summary>
        public LoadBalancingModeEnum LoadBalancingMode { get; set; } = LoadBalancingModeEnum.RoundRobin;

        /// <summary>
        /// Maximum heartbeat age in milliseconds for a connected client to remain eligible for routing. Defaults to 60000.
        /// </summary>
        public int ClientFreshnessTimeoutMs
        {
            get
            {
                return _ClientFreshnessTimeoutMs;
            }
            set
            {
                _ClientFreshnessTimeoutMs = System.Math.Clamp(value, 5000, 600000);
            }
        }

        /// <summary>
        /// Adaptive selection settings.
        /// </summary>
        public AdaptiveLoadBalancingSettings Adaptive { get; set; } = new AdaptiveLoadBalancingSettings();
    }
}

namespace Xeno.Core.Settings
{
    /// <summary>
    /// Adaptive load-balancing settings for connected-client routing.
    /// </summary>
    public class AdaptiveLoadBalancingSettings
    {
        /// <summary>
        /// Number of eligible clients sampled before scoring.
        /// </summary>
        public int SampleCount
        {
            get
            {
                return _SampleCount;
            }
            set
            {
                _SampleCount = value < 1 ? 1 : value > 8 ? 8 : value;
            }
        }

        /// <summary>
        /// Weight for the available-capacity score.
        /// </summary>
        public double CapacityWeight { get; set; } = 45;

        /// <summary>
        /// Weight for the current in-flight workload score.
        /// </summary>
        public double InFlightWeight { get; set; } = 30;

        /// <summary>
        /// Weight for the configured client routing weight score.
        /// </summary>
        public double ClientWeight { get; set; } = 15;

        /// <summary>
        /// Weight for heartbeat freshness.
        /// </summary>
        public double HeartbeatWeight { get; set; } = 10;

        private int _SampleCount = 2;
    }
}

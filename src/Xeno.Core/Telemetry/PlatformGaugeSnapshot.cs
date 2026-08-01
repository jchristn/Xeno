namespace Xeno.Core.Telemetry
{
    /// <summary>
    /// Point-in-time platform values sampled by observable gauges.
    /// </summary>
    public class PlatformGaugeSnapshot
    {
        /// <summary>
        /// Number of currently connected clients.
        /// </summary>
        public int ConnectedClients { get; set; } = 0;

        /// <summary>
        /// Number of distinct models advertised by connected clients.
        /// </summary>
        public int AvailableModels { get; set; } = 0;

        /// <summary>
        /// Number of in-flight relayed requests.
        /// </summary>
        public int InflightRequests { get; set; } = 0;

        /// <summary>
        /// Current ledger queue depth.
        /// </summary>
        public int LedgerQueueDepth { get; set; } = 0;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public PlatformGaugeSnapshot()
        {
        }
    }
}

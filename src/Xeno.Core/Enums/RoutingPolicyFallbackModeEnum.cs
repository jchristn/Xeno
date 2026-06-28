namespace Xeno.Core.Enums
{
    /// <summary>
    /// Routing policy telemetry fallback behavior.
    /// </summary>
    public enum RoutingPolicyFallbackModeEnum
    {
        /// <summary>
        /// Use the policy load-balancing mode if telemetry cannot select a client.
        /// </summary>
        UseLoadBalancingMode,

        /// <summary>
        /// Fail routing when telemetry cannot select a client.
        /// </summary>
        FailClosed
    }
}

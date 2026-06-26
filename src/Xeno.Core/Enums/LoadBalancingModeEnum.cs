namespace Xeno.Core.Enums
{
    /// <summary>
    /// Load-balancing mode for selecting a connected Xeno client.
    /// </summary>
    public enum LoadBalancingModeEnum
    {
        /// <summary>
        /// Weighted round-robin selection.
        /// </summary>
        RoundRobin = 0,

        /// <summary>
        /// Weighted random selection.
        /// </summary>
        Random = 1,

        /// <summary>
        /// Select the first eligible client after deterministic candidate ordering.
        /// </summary>
        FirstAvailable = 2,

        /// <summary>
        /// Select the eligible client least recently assigned for the route scope.
        /// </summary>
        LeastRecentlyUsed = 3,

        /// <summary>
        /// Select by adaptive runtime score using Xeno client capacity and telemetry.
        /// </summary>
        Adaptive = 4
    }
}

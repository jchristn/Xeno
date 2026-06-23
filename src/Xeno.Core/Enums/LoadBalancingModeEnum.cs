namespace Xeno.Core.Enums
{

    /// <summary>
    /// Built-in load-balancing mode.
    /// </summary>
    public enum LoadBalancingModeEnum
    {
        /// <summary>
        /// Rotate through candidates.
        /// </summary>
        RoundRobin = 0,

        /// <summary>
        /// Select randomly.
        /// </summary>
        Random = 1,

        /// <summary>
        /// Select first eligible endpoint.
        /// </summary>
        FirstAvailable = 2
    }
}

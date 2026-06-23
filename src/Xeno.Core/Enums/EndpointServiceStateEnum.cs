namespace Xeno.Core.Enums
{

    /// <summary>
    /// Endpoint service state.
    /// </summary>
    public enum EndpointServiceStateEnum
    {
        /// <summary>
        /// Endpoint is eligible for new routing.
        /// </summary>
        Normal = 0,

        /// <summary>
        /// Endpoint only serves existing sticky-session pins.
        /// </summary>
        Draining = 1,

        /// <summary>
        /// Endpoint is excluded from all routing.
        /// </summary>
        Quarantined = 2
    }
}

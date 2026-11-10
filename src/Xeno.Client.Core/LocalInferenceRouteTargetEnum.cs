namespace Xeno.Client
{
    /// <summary>
    /// Where a local inference route rule sends a model.
    /// </summary>
    public enum LocalInferenceRouteTargetEnum
    {
        /// <summary>
        /// A named provider configured on this machine.
        /// </summary>
        Local,

        /// <summary>
        /// The Xeno network through the websocket tunnel.
        /// </summary>
        Network
    }
}

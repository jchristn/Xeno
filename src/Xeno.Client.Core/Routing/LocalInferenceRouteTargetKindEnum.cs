namespace Xeno.Client.Routing
{
    /// <summary>
    /// Where the router decided to send a request.
    /// </summary>
    public enum LocalInferenceRouteTargetKindEnum
    {
        /// <summary>
        /// A provider configured on this machine.
        /// </summary>
        Local,

        /// <summary>
        /// The Xeno network through the websocket tunnel.
        /// </summary>
        Network,

        /// <summary>
        /// Nowhere: the routing mode or rules leave no target for the model.
        /// </summary>
        Unavailable
    }
}

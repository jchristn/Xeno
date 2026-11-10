namespace Xeno.Client
{
    /// <summary>
    /// How the local inference listener chooses between local providers and the Xeno network.
    /// </summary>
    public enum LocalInferenceRoutingModeEnum
    {
        /// <summary>
        /// Send a request to a healthy local provider that serves the model, otherwise to the Xeno network. Default.
        /// </summary>
        PreferLocal,

        /// <summary>
        /// Send every request to the Xeno network, as releases before 0.2.0 did.
        /// </summary>
        NetworkOnly,

        /// <summary>
        /// Send requests only to local providers and never use the Xeno network.
        /// </summary>
        LocalOnly
    }
}

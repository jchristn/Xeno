namespace Xeno.Core.Analytics
{
    /// <summary>
    /// A stream of records usage analytics can chart.
    /// </summary>
    public enum UsageDatasetEnum
    {
        /// <summary>
        /// Requests the Xeno server relayed to clients.
        /// </summary>
        NetworkRequests,

        /// <summary>
        /// Network requests this machine answered for the Xeno network.
        /// </summary>
        ServedRequests,

        /// <summary>
        /// Requests to this machine's local inference endpoint.
        /// </summary>
        LocalEndpoint,

        /// <summary>
        /// Chat turns.
        /// </summary>
        Chat
    }
}

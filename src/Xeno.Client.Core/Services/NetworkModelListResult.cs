namespace Xeno.Client.Services
{
    using System.Collections.Generic;

    /// <summary>
    /// Result of asking the Xeno server which models the network offers this client.
    /// </summary>
    public class NetworkModelListResult
    {
        /// <summary>
        /// True when the server returned a model list.
        /// </summary>
        public bool Success { get; set; } = false;

        /// <summary>
        /// Models offered by the network.
        /// </summary>
        public List<NetworkModelEntry> Models { get; set; } = new List<NetworkModelEntry>();

        /// <summary>
        /// HTTP status code, or null when the request did not complete.
        /// </summary>
        public int? StatusCode { get; set; } = null;

        /// <summary>
        /// Failure description, or null on success.
        /// </summary>
        public string? Error { get; set; } = null;
    }
}

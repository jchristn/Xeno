namespace Xeno.Client.LocalDashboard.Models
{
    /// <summary>
    /// Local request timeline stage.
    /// </summary>
    public enum LocalRequestTimelineStageEnum
    {
        /// <summary>
        /// Request was received from the Xeno server.
        /// </summary>
        Received = 0,

        /// <summary>
        /// Provider endpoint was selected.
        /// </summary>
        ProviderSelected = 1,

        /// <summary>
        /// Upstream request was sent.
        /// </summary>
        UpstreamRequest = 2,

        /// <summary>
        /// Upstream response headers were received.
        /// </summary>
        UpstreamHeaders = 3,

        /// <summary>
        /// First response byte or token was received.
        /// </summary>
        FirstByte = 4,

        /// <summary>
        /// Request completed.
        /// </summary>
        Completed = 5,

        /// <summary>
        /// Request failed.
        /// </summary>
        Failed = 6
    }
}

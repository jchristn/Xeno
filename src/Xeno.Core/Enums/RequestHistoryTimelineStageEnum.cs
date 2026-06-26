namespace Xeno.Core.Enums
{

    /// <summary>
    /// Stable request-history timeline stage.
    /// </summary>
    public enum RequestHistoryTimelineStageEnum
    {
        /// <summary>
        /// Unknown or unclassified stage.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// API request received by the Xeno server.
        /// </summary>
        Received = 1,

        /// <summary>
        /// Requested model was resolved.
        /// </summary>
        ModelResolution = 2,

        /// <summary>
        /// Client inventory was evaluated.
        /// </summary>
        ClientInventory = 3,

        /// <summary>
        /// Connected client was selected.
        /// </summary>
        ClientSelection = 4,

        /// <summary>
        /// Selected client could not be found.
        /// </summary>
        ClientMissing = 5,

        /// <summary>
        /// Selected client was not connected.
        /// </summary>
        ClientNotConnected = 6,

        /// <summary>
        /// Request was dispatched to the selected client.
        /// </summary>
        ClientDispatch = 7,

        /// <summary>
        /// Selected client started a streamed response.
        /// </summary>
        ClientResponseStart = 8,

        /// <summary>
        /// Selected client returned a non-streamed response.
        /// </summary>
        ClientResponse = 9,

        /// <summary>
        /// Selected client completed a streamed response.
        /// </summary>
        ClientResponseComplete = 10,

        /// <summary>
        /// Selected client did not respond before timeout.
        /// </summary>
        ClientTimeout = 11,

        /// <summary>
        /// Upstream response headers were received.
        /// </summary>
        ResponseHeaders = 12,

        /// <summary>
        /// First streamed byte or token was received.
        /// </summary>
        FirstToken = 13,

        /// <summary>
        /// Request completed successfully.
        /// </summary>
        Completed = 14,

        /// <summary>
        /// Request failed.
        /// </summary>
        Failed = 15,

        /// <summary>
        /// Client stream was active.
        /// </summary>
        Streaming = 16
    }
}

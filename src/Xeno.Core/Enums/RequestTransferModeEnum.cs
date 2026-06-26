namespace Xeno.Core.Enums
{

    /// <summary>
    /// Transfer mode observed while relaying a request.
    /// </summary>
    public enum RequestTransferModeEnum
    {
        /// <summary>
        /// Transfer mode is not known.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Request was relayed as a buffered response.
        /// </summary>
        Standard = 1,

        /// <summary>
        /// Request was relayed as a streamed response.
        /// </summary>
        Stream = 2
    }
}

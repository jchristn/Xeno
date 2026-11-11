namespace Xeno.Core.Chat
{
    /// <summary>
    /// Outcome of a chat turn.
    /// </summary>
    public enum ChatTurnStatusEnum
    {
        /// <summary>
        /// The turn is still running.
        /// </summary>
        Running,

        /// <summary>
        /// The model answered.
        /// </summary>
        Success,

        /// <summary>
        /// The turn failed; see the turn's error.
        /// </summary>
        Error,

        /// <summary>
        /// The user stopped the turn.
        /// </summary>
        Cancelled
    }
}

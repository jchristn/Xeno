namespace Xeno.Core.Chat
{
    /// <summary>
    /// Why a model did not answer, or stopped answering. Every chat surface shows it in the conversation.
    /// </summary>
    public enum ChatFailureKindEnum
    {
        /// <summary>
        /// The model finished without writing an answer.
        /// </summary>
        NoResponse,

        /// <summary>
        /// The model, its provider, or the Xeno network took too long.
        /// </summary>
        Timeout,

        /// <summary>
        /// The model could not be reached: its provider is down or busy, or the Xeno network is not connected.
        /// </summary>
        Unavailable,

        /// <summary>
        /// The model or its provider returned an error.
        /// </summary>
        ModelError,

        /// <summary>
        /// The answer stopped partway because the connection or the provider failed.
        /// </summary>
        Interrupted,

        /// <summary>
        /// The user stopped the answer.
        /// </summary>
        Stopped,

        /// <summary>
        /// Something else went wrong.
        /// </summary>
        Failed
    }
}

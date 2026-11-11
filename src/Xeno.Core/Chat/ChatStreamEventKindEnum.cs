namespace Xeno.Core.Chat
{
    /// <summary>
    /// Kind of event streamed while a chat turn runs. The SSE event name is the lower-case name.
    /// </summary>
    public enum ChatStreamEventKindEnum
    {
        /// <summary>
        /// The turn started; carries the conversation, turn, and user message ids.
        /// </summary>
        Turn,

        /// <summary>
        /// Reasoning text from the model.
        /// </summary>
        Thinking,

        /// <summary>
        /// Answer text from the model.
        /// </summary>
        Chunk,

        /// <summary>
        /// A tool call changed state.
        /// </summary>
        ToolCall,

        /// <summary>
        /// The finished turn, with token counts and timing.
        /// </summary>
        Usage,

        /// <summary>
        /// The turn failed.
        /// </summary>
        Error,

        /// <summary>
        /// The stream is over.
        /// </summary>
        Done
    }
}

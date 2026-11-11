namespace Xeno.Core.Chat
{
    using System;

    /// <summary>
    /// Tool result sent back to the model when a tool did not run or failed.
    /// </summary>
    public sealed class ChatToolErrorResult
    {
        /// <summary>
        /// What went wrong.
        /// </summary>
        public string Error { get; set; } = String.Empty;
    }
}

namespace Xeno.Core.Chat
{
    /// <summary>
    /// Answer to a tool call waiting for approval.
    /// </summary>
    public sealed class ChatToolApprovalRequest
    {
        /// <summary>
        /// True to run the tool, false to deny it.
        /// </summary>
        public bool Allow { get; set; } = false;
    }
}

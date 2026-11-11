namespace Xeno.Client.Tools.Workspace
{
    using System;

    /// <summary>
    /// Where a workspace tool call runs.
    /// </summary>
    public sealed class WorkspaceToolContext
    {
        /// <summary>
        /// Conversation id; tools that keep state (task plans, background processes) keep it per conversation.
        /// </summary>
        public string ConversationId { get; set; } = String.Empty;

        /// <summary>
        /// Turn id.
        /// </summary>
        public string TurnId { get; set; } = String.Empty;

        /// <summary>
        /// Absolute, existing working directory. Tools may not reach outside it.
        /// </summary>
        public string WorkingDirectory { get; set; } = String.Empty;

        /// <summary>
        /// Chat settings in effect for the call.
        /// </summary>
        public ClientChatSettings Settings { get; set; } = new ClientChatSettings();
    }
}

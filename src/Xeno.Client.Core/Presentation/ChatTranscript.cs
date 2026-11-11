namespace Xeno.Client.Presentation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Xeno.Client.Chat;
    using Xeno.Core.Chat;

    /// <summary>
    /// Builds the Markdown for a chat transcript in the terminal app. Every message is escaped, so model output is shown
    /// literally rather than interpreted.
    /// </summary>
    public static class ChatTranscript
    {
        /// <summary>
        /// Marker before a failure line in the terminal transcript.
        /// </summary>
        public const string FailureMarker = "(!)";

        /// <summary>
        /// Markdown for a conversation, with an optional answer still streaming at the end.
        /// </summary>
        /// <param name="conversation">Conversation, or null for a new chat.</param>
        /// <param name="pendingUser">A message just sent, not yet in the conversation, or null.</param>
        /// <param name="live">Answer text streaming now, or null.</param>
        /// <param name="liveStatus">Status line for the streaming answer, or null.</param>
        /// <param name="liveTools">Tool call lines for the streaming answer.</param>
        /// <returns>Markdown.</returns>
        public static string Build(ChatConversation? conversation, string? pendingUser, string? live, string? liveStatus, IEnumerable<string>? liveTools = null)
        {
            List<string> blocks = new List<string>();
            List<ChatMessage> messages = conversation?.Messages ?? new List<ChatMessage>();
            foreach (ChatMessage message in messages)
            {
                if (message.Role == ChatRoleEnum.User)
                {
                    blocks.Add("**You**");
                    blocks.Add(XenoMarkdown.EscapeParagraphs(message.Content));
                }
                else if (message.Role == ChatRoleEnum.Assistant)
                {
                    blocks.Add("**" + XenoMarkdown.Escape(message.Model ?? "Model") + "**");
                    foreach (ChatToolCallRecord call in message.ToolCalls) blocks.Add("> " + XenoMarkdown.Escape(ToolLine(call)));
                    if (!String.IsNullOrEmpty(message.Content)) blocks.Add(XenoMarkdown.EscapeParagraphs(message.Content));
                    string? failure = ChatFormat.FailureLine(message);
                    if (failure != null) blocks.Add("> **" + XenoMarkdown.Escape(FailureMarker) + "** " + XenoMarkdown.Escape(failure));
                    string meta = ChatFormat.AnswerMeta(message);
                    if (message.Feedback != null) meta += " · rated " + (message.Feedback.Rating > 0 ? "up" : "down") + (String.IsNullOrWhiteSpace(message.Feedback.Comment) ? String.Empty : ": " + message.Feedback.Comment);
                    blocks.Add("*" + XenoMarkdown.Escape(meta) + "*  \n" + XenoMarkdown.Escape(ChatFormat.TelemetryLine(message)));
                }
            }

            if (!String.IsNullOrEmpty(pendingUser))
            {
                blocks.Add("**You**");
                blocks.Add(XenoMarkdown.EscapeParagraphs(pendingUser));
            }

            if (live != null || liveStatus != null)
            {
                blocks.Add("**" + XenoMarkdown.Escape(conversation?.Model ?? "Model") + "**");
                foreach (string tool in liveTools ?? Enumerable.Empty<string>()) blocks.Add("> " + XenoMarkdown.Escape(tool));
                if (!String.IsNullOrEmpty(live)) blocks.Add(XenoMarkdown.EscapeParagraphs(live));
                if (!String.IsNullOrEmpty(liveStatus)) blocks.Add("*" + XenoMarkdown.Escape(liveStatus) + "*");
            }

            if (blocks.Count == 0) return "Pick a model and type a message below. Answers stream in as the model writes them.";
            return String.Join("\n\n", blocks);
        }

        /// <summary>
        /// One line describing a tool call.
        /// </summary>
        /// <param name="call">Tool call.</param>
        /// <returns>Text.</returns>
        /// <exception cref="ArgumentNullException">call is null.</exception>
        public static string ToolLine(ChatToolCallRecord call)
        {
            ArgumentNullException.ThrowIfNull(call);
            string state = call.Approval == ChatToolApprovalEnum.Pending ? "waiting for approval"
                : call.Success ? "done in " + ChatFormat.Duration(call.DurationMs)
                : call.Error != null ? "not run: " + call.Error
                : "running";
            return "Tool " + call.ToolName + " " + call.Arguments + ": " + state;
        }
    }
}

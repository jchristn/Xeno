namespace Xeno.Client.Tools
{
    using System;
    using Xeno.Client.Chat;
    using Xeno.Core.Chat;

    /// <summary>
    /// Builds xeno_chat results.
    /// </summary>
    public static class XenoChatResults
    {
        /// <summary>
        /// Build a result from a finished turn.
        /// </summary>
        /// <param name="conversationId">Conversation id.</param>
        /// <param name="turn">Turn, or null when none came back.</param>
        /// <param name="answer">Answer text.</param>
        /// <param name="error">Error streamed during the turn, or null.</param>
        /// <param name="failure">Why the model did not answer, or null.</param>
        /// <returns>Result.</returns>
        public static XenoChatToolResult Build(string conversationId, ChatTurn? turn, string answer, string? error, ChatFailure? failure = null)
        {
            return new XenoChatToolResult
            {
                ConversationId = conversationId,
                TurnId = turn?.Id ?? String.Empty,
                Model = turn?.Model ?? String.Empty,
                Route = turn?.Route ?? String.Empty,
                Answer = answer,
                Status = turn?.Status.ToString() ?? ChatTurnStatusEnum.Error.ToString(),
                Error = error ?? turn?.Error ?? (turn == null ? "The chat ended without an answer." : null),
                FailureKind = failure?.Kind.ToString(),
                TotalMs = turn?.TotalMs ?? 0
            };
        }
    }
}

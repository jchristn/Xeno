namespace Xeno.Core.Chat
{
    using System;

    /// <summary>
    /// Why an answer is missing or incomplete, saved on the answer so every chat surface can say so in the conversation.
    /// </summary>
    public sealed class ChatFailure
    {
        #region Public-Members

        /// <summary>
        /// Kind of failure.
        /// </summary>
        public ChatFailureKindEnum Kind { get; set; } = ChatFailureKindEnum.Failed;

        /// <summary>
        /// What went wrong, in plain words (for example the provider's error or how long Xeno waited).
        /// </summary>
        public string Reason { get; set; } = String.Empty;

        /// <summary>
        /// HTTP status the model or route returned, when there was one.
        /// </summary>
        public int? StatusCode { get; set; } = null;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Short heading for a failure, shown before its reason.
        /// </summary>
        /// <param name="kind">Kind of failure.</param>
        /// <param name="partial">True when part of the answer arrived before the failure.</param>
        /// <returns>Text.</returns>
        public static string Headline(ChatFailureKindEnum kind, bool partial)
        {
            switch (kind)
            {
                case ChatFailureKindEnum.NoResponse: return "No response from the model";
                case ChatFailureKindEnum.Timeout: return partial ? "The answer timed out" : "The model timed out";
                case ChatFailureKindEnum.Unavailable: return "The model is unavailable";
                case ChatFailureKindEnum.ModelError: return "The model returned an error";
                case ChatFailureKindEnum.Interrupted: return "The answer was cut off";
                case ChatFailureKindEnum.Stopped: return partial ? "Stopped" : "Stopped before the model answered";
                default: return "The model did not answer";
            }
        }

        /// <summary>
        /// One line: the headline and, when there is one, the reason.
        /// </summary>
        /// <param name="partial">True when part of the answer arrived before the failure.</param>
        /// <returns>Text.</returns>
        public string Describe(bool partial)
        {
            string headline = Headline(Kind, partial);
            if (String.IsNullOrWhiteSpace(Reason)) return headline + ".";
            string reason = Reason.Trim();
            return headline + ": " + reason + (reason.EndsWith('.') || reason.EndsWith('!') || reason.EndsWith('?') ? String.Empty : ".");
        }

        #endregion
    }
}

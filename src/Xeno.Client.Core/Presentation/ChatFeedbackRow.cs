namespace Xeno.Client.Presentation
{
    using System;
    using Xeno.Client.Chat;
    using Xeno.Core.Chat;

    /// <summary>
    /// A feedback row for the desktop and terminal tables.
    /// </summary>
    public sealed class ChatFeedbackRow
    {
        /// <summary>When it was rated.</summary>
        public string When { get; set; } = String.Empty;

        /// <summary>Rating.</summary>
        public string Rating { get; set; } = String.Empty;

        /// <summary>Comment.</summary>
        public string Comment { get; set; } = String.Empty;

        /// <summary>Model.</summary>
        public string Model { get; set; } = String.Empty;

        /// <summary>Where the answer ran.</summary>
        public string Route { get; set; } = String.Empty;

        /// <summary>Conversation title.</summary>
        public string Conversation { get; set; } = String.Empty;

        /// <summary>Start of the answer.</summary>
        public string Excerpt { get; set; } = String.Empty;

        /// <summary>The feedback.</summary>
        public ChatFeedback Feedback { get; set; } = new ChatFeedback();

        /// <summary>
        /// Build a row.
        /// </summary>
        /// <param name="feedback">Feedback.</param>
        /// <returns>Row.</returns>
        /// <exception cref="ArgumentNullException">feedback is null.</exception>
        public static ChatFeedbackRow From(ChatFeedback feedback)
        {
            ArgumentNullException.ThrowIfNull(feedback);
            string excerpt = (feedback.Excerpt ?? String.Empty).Replace('\n', ' ').Replace('\r', ' ');
            return new ChatFeedbackRow
            {
                When = ChatFormat.When(feedback.UpdatedUtc),
                Rating = ChatFormat.Rating(feedback.Rating),
                Comment = feedback.Comment ?? String.Empty,
                Model = feedback.Model ?? "-",
                Route = ChatFormat.Route(feedback.Route),
                Conversation = feedback.ConversationTitle ?? "(deleted)",
                Excerpt = excerpt.Length > 120 ? excerpt.Substring(0, 119) + "…" : excerpt,
                Feedback = feedback
            };
        }
    }
}

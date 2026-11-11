namespace Xeno.Client.Presentation
{
    using System;
    using System.Globalization;
    using Xeno.Client.Chat;
    using Xeno.Core.Chat;

    /// <summary>
    /// A chat history row for the desktop and terminal tables.
    /// </summary>
    public sealed class ChatTurnRow
    {
        /// <summary>When the turn started.</summary>
        public string When { get; set; } = String.Empty;

        /// <summary>Conversation title.</summary>
        public string Conversation { get; set; } = String.Empty;

        /// <summary>Model.</summary>
        public string Model { get; set; } = String.Empty;

        /// <summary>Where it ran.</summary>
        public string Route { get; set; } = String.Empty;

        /// <summary>Outcome.</summary>
        public string Outcome { get; set; } = String.Empty;

        /// <summary>Total time.</summary>
        public string Time { get; set; } = String.Empty;

        /// <summary>Completion tokens.</summary>
        public string Tokens { get; set; } = String.Empty;

        /// <summary>Rating.</summary>
        public string Rating { get; set; } = String.Empty;

        /// <summary>Where the chat came from.</summary>
        public string Source { get; set; } = String.Empty;

        /// <summary>The turn.</summary>
        public ChatTurn Turn { get; set; } = new ChatTurn();

        /// <summary>
        /// Build a row.
        /// </summary>
        /// <param name="turn">Turn.</param>
        /// <returns>Row.</returns>
        /// <exception cref="ArgumentNullException">turn is null.</exception>
        public static ChatTurnRow From(ChatTurn turn)
        {
            ArgumentNullException.ThrowIfNull(turn);
            return new ChatTurnRow
            {
                When = ChatFormat.When(turn.CreatedUtc),
                Conversation = turn.ConversationTitle ?? "(deleted)",
                Model = turn.Model,
                Route = ChatFormat.Route(turn.Route, turn.Fallback),
                Outcome = ChatFormat.Outcome(turn.Status),
                Time = ChatFormat.Duration(turn.TotalMs),
                Tokens = turn.CompletionTokens.HasValue ? turn.CompletionTokens.Value.ToString(CultureInfo.InvariantCulture) : "-",
                Rating = ChatFormat.Rating(turn.FeedbackRating),
                Source = ChatFormat.Surface(turn.Surface),
                Turn = turn
            };
        }
    }
}

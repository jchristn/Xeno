namespace Xeno.Core.Chat
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Storage for conversations, messages, tool calls, feedback, and chat request history.
    /// </summary>
    public interface IChatStore
    {
        /// <summary>
        /// Create tables when missing.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task InitializeAsync(CancellationToken token);

        /// <summary>
        /// Save a new conversation. An empty id is replaced with a new one.
        /// </summary>
        /// <param name="conversation">Conversation.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The saved conversation.</returns>
        Task<ChatConversation> CreateConversationAsync(ChatConversation conversation, CancellationToken token);

        /// <summary>
        /// Read a conversation.
        /// </summary>
        /// <param name="id">Conversation id.</param>
        /// <param name="includeMessages">True to load messages, their tool calls, and their feedback.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Conversation, or null.</returns>
        Task<ChatConversation?> GetConversationAsync(string id, bool includeMessages, CancellationToken token);

        /// <summary>
        /// List conversations, pinned first, then most recently changed.
        /// </summary>
        /// <param name="search">Text to find in titles and models, or null.</param>
        /// <param name="surface">Surface, or null for all.</param>
        /// <param name="limit">Maximum to return, 1 to 1000.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Conversations without messages.</returns>
        Task<List<ChatConversation>> ListConversationsAsync(string? search, ChatSurfaceEnum? surface, int limit, CancellationToken token);

        /// <summary>
        /// Save a conversation's title, model, system prompt, tool switch, and pin.
        /// </summary>
        /// <param name="conversation">Conversation.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when it existed.</returns>
        Task<bool> UpdateConversationAsync(ChatConversation conversation, CancellationToken token);

        /// <summary>
        /// Delete a conversation with its messages, tool calls, and feedback. Its request history rows stay.
        /// </summary>
        /// <param name="id">Conversation id.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when it existed.</returns>
        Task<bool> DeleteConversationAsync(string id, CancellationToken token);

        /// <summary>
        /// Append a message, giving it the next sequence number and a new id when empty.
        /// </summary>
        /// <param name="message">Message.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The saved message.</returns>
        Task<ChatMessage> AddMessageAsync(ChatMessage message, CancellationToken token);

        /// <summary>
        /// Save a message's content, thinking, route, usage, and timing.
        /// </summary>
        /// <param name="message">Message.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task UpdateMessageAsync(ChatMessage message, CancellationToken token);

        /// <summary>
        /// Read a message.
        /// </summary>
        /// <param name="id">Message id.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Message, or null.</returns>
        Task<ChatMessage?> GetMessageAsync(string id, CancellationToken token);

        /// <summary>
        /// Insert or replace a tool call.
        /// </summary>
        /// <param name="call">Tool call.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task UpsertToolCallAsync(ChatToolCallRecord call, CancellationToken token);

        /// <summary>
        /// Insert or replace a turn.
        /// </summary>
        /// <param name="turn">Turn.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task UpsertTurnAsync(ChatTurn turn, CancellationToken token);

        /// <summary>
        /// Read a turn with its tool calls.
        /// </summary>
        /// <param name="id">Turn id.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Turn, or null.</returns>
        Task<ChatTurn?> GetTurnAsync(string id, CancellationToken token);

        /// <summary>
        /// Query chat request history, newest first.
        /// </summary>
        /// <param name="query">Filters and paging.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Page.</returns>
        Task<ChatPage<ChatTurn>> QueryTurnsAsync(ChatHistoryQuery query, CancellationToken token);

        /// <summary>
        /// Delete the turns matching a query's filters (paging is ignored), with their tool calls.
        /// </summary>
        /// <param name="query">Filters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Turns removed.</returns>
        Task<int> DeleteTurnsAsync(ChatHistoryQuery query, CancellationToken token);

        /// <summary>
        /// Delete turns and their tool calls that started before a time.
        /// </summary>
        /// <param name="olderThanUtc">Cutoff, UTC.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Turns removed.</returns>
        Task<int> PruneTurnsAsync(DateTime olderThanUtc, CancellationToken token);

        /// <summary>
        /// Rate a message, replacing any earlier rating.
        /// </summary>
        /// <param name="messageId">Assistant message id.</param>
        /// <param name="request">Rating and comment.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Saved feedback.</returns>
        /// <exception cref="KeyNotFoundException">The message does not exist.</exception>
        /// <exception cref="ArgumentException">The rating is not 1 or -1, or the message is not an assistant message.</exception>
        Task<ChatFeedback> SetFeedbackAsync(string messageId, ChatFeedbackRequest request, CancellationToken token);

        /// <summary>
        /// Remove a message's rating.
        /// </summary>
        /// <param name="messageId">Message id.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when there was one.</returns>
        Task<bool> DeleteFeedbackAsync(string messageId, CancellationToken token);

        /// <summary>
        /// Query feedback, newest first.
        /// </summary>
        /// <param name="query">Filters and paging.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Page.</returns>
        Task<ChatPage<ChatFeedback>> QueryFeedbackAsync(ChatFeedbackQuery query, CancellationToken token);

        /// <summary>
        /// Chat activity over a range.
        /// </summary>
        /// <param name="fromUtc">Start, UTC.</param>
        /// <param name="toUtc">End, UTC.</param>
        /// <param name="bucketSeconds">Bucket size in seconds.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Analytics.</returns>
        Task<ChatAnalytics> AnalyticsAsync(DateTime fromUtc, DateTime toUtc, int bucketSeconds, CancellationToken token);

        /// <summary>
        /// Usage records for finished turns in a range, read from typed columns, for usage analytics.
        /// </summary>
        /// <param name="fromUtc">Start, UTC, inclusive.</param>
        /// <param name="toUtc">End, UTC, exclusive.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Records.</returns>
        System.Collections.Generic.IAsyncEnumerable<Xeno.Core.Analytics.UsageRecord> EnumerateUsageAsync(DateTime fromUtc, DateTime toUtc, CancellationToken token);
    }
}

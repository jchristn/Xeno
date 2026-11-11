namespace Xeno.Client.Tools.Workspace.Planning
{
    using System;
    using System.Collections.Concurrent;

    /// <summary>
    /// Task plans kept in memory, one per conversation id. Thread safe.
    /// </summary>
    public sealed class TaskPlanStore
    {
        #region Private-Members

        private readonly ConcurrentDictionary<string, TaskPlan> _Plans = new ConcurrentDictionary<string, TaskPlan>(StringComparer.Ordinal);

        #endregion

        #region Public-Methods

        /// <summary>
        /// The plan for a conversation.
        /// </summary>
        /// <param name="conversationId">Conversation id.</param>
        /// <returns>Plan, or null when the conversation has never had one.</returns>
        public TaskPlan? Get(string conversationId)
        {
            if (String.IsNullOrEmpty(conversationId)) return null;
            return _Plans.TryGetValue(conversationId, out TaskPlan? plan) ? plan : null;
        }

        /// <summary>
        /// The plan for a conversation, created empty when it has none.
        /// </summary>
        /// <param name="conversationId">Conversation id.</param>
        /// <returns>Plan.</returns>
        /// <exception cref="ArgumentException">conversationId is null or empty.</exception>
        public TaskPlan GetOrCreate(string conversationId)
        {
            if (String.IsNullOrEmpty(conversationId)) throw new ArgumentException("A conversation id is required.", nameof(conversationId));
            return _Plans.GetOrAdd(conversationId, delegate (string key) { return new TaskPlan(); });
        }

        /// <summary>
        /// Forget a conversation's plan, for example when the conversation is deleted.
        /// </summary>
        /// <param name="conversationId">Conversation id.</param>
        public void Remove(string conversationId)
        {
            if (String.IsNullOrEmpty(conversationId)) return;
            _Plans.TryRemove(conversationId, out TaskPlan? removed);
        }

        #endregion
    }
}

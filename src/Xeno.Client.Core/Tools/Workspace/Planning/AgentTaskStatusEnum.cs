namespace Xeno.Client.Tools.Workspace.Planning
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Lifecycle status of a single task within a conversation's task plan.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<AgentTaskStatusEnum>))]
    public enum AgentTaskStatusEnum
    {
        /// <summary>
        /// The task has not been started. Its dependencies may or may not be satisfied.
        /// </summary>
        [JsonStringEnumMemberName("pending")]
        Pending,

        /// <summary>
        /// The task is actively being worked. At most one task should hold this status.
        /// </summary>
        [JsonStringEnumMemberName("in_progress")]
        InProgress,

        /// <summary>
        /// The task finished successfully.
        /// </summary>
        [JsonStringEnumMemberName("completed")]
        Completed,

        /// <summary>
        /// The task was attempted and failed. A failed task always carries an explanatory note.
        /// </summary>
        [JsonStringEnumMemberName("failed")]
        Failed,

        /// <summary>
        /// The task was intentionally not done: no longer needed or superseded by a re-plan.
        /// </summary>
        [JsonStringEnumMemberName("skipped")]
        Skipped,

        /// <summary>
        /// The task cannot proceed yet because it is waiting on a dependency or an external condition the model has
        /// flagged. Distinct from <see cref="Pending"/>, which is simply not yet started.
        /// </summary>
        [JsonStringEnumMemberName("blocked")]
        Blocked
    }
}

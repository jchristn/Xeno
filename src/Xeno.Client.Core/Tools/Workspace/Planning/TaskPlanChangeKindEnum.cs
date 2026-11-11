namespace Xeno.Client.Tools.Workspace.Planning
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// What changed about a task plan in its most recent mutation.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<TaskPlanChangeKindEnum>))]
    public enum TaskPlanChangeKindEnum
    {
        /// <summary>
        /// A plan was established on a conversation that previously had none.
        /// </summary>
        [JsonStringEnumMemberName("plan_created")]
        PlanCreated,

        /// <summary>
        /// An existing plan was replaced wholesale by a new set of tasks.
        /// </summary>
        [JsonStringEnumMemberName("plan_replaced")]
        PlanReplaced,

        /// <summary>
        /// A single task's status changed.
        /// </summary>
        [JsonStringEnumMemberName("task_status_changed")]
        TaskStatusChanged,

        /// <summary>
        /// A single task's note changed without a status transition.
        /// </summary>
        [JsonStringEnumMemberName("task_note_updated")]
        TaskNoteUpdated,

        /// <summary>
        /// The plan was cleared and the conversation no longer has any tasks.
        /// </summary>
        [JsonStringEnumMemberName("plan_cleared")]
        PlanCleared
    }
}

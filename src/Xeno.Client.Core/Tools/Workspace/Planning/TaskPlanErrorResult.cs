namespace Xeno.Client.Tools.Workspace.Planning
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Error returned to the model by plan_tasks or update_task so it can correct the call.
    /// </summary>
    public sealed class TaskPlanErrorResult
    {
        /// <summary>
        /// Error code: task_planning_disabled, invalid_plan, invalid_argument, invalid_status, note_required, or
        /// unknown_task.
        /// </summary>
        [JsonPropertyName("error")]
        public string Error { get; set; } = String.Empty;

        /// <summary>
        /// Explanation, or null for invalid_plan.
        /// </summary>
        [JsonPropertyName("message")]
        public string? Message { get; set; } = null;

        /// <summary>
        /// Validation problems for invalid_plan, otherwise null.
        /// </summary>
        [JsonPropertyName("problems")]
        public List<string>? Problems { get; set; } = null;

        /// <summary>
        /// Ids in the current plan for unknown_task, otherwise null.
        /// </summary>
        [JsonPropertyName("validIds")]
        public List<string>? ValidIds { get; set; } = null;
    }
}

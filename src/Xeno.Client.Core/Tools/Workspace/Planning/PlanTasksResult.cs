namespace Xeno.Client.Tools.Workspace.Planning
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Result of a successful plan_tasks call.
    /// </summary>
    public sealed class PlanTasksResult
    {
        /// <summary>
        /// Always true.
        /// </summary>
        [JsonPropertyName("ok")]
        public bool Ok { get; set; } = true;

        /// <summary>
        /// Number of tasks in the new plan.
        /// </summary>
        [JsonPropertyName("taskCount")]
        public int TaskCount { get; set; } = 0;
    }
}

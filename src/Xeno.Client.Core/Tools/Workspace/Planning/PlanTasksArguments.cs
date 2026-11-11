namespace Xeno.Client.Tools.Workspace.Planning
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Arguments of plan_tasks.
    /// </summary>
    public sealed class PlanTasksArguments
    {
        /// <summary>
        /// Ordered tasks that make up the plan.
        /// </summary>
        [JsonPropertyName("tasks")]
        public List<PlanTaskArgument?>? Tasks { get; set; } = null;
    }
}

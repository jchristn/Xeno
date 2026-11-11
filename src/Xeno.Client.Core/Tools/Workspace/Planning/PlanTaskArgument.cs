namespace Xeno.Client.Tools.Workspace.Planning
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// One task in the plan_tasks arguments.
    /// </summary>
    public sealed class PlanTaskArgument
    {
        /// <summary>
        /// Short stable id unique within the plan.
        /// </summary>
        [JsonPropertyName("id")]
        public string? Id { get; set; } = null;

        /// <summary>
        /// Short imperative label.
        /// </summary>
        [JsonPropertyName("title")]
        public string? Title { get; set; } = null;

        /// <summary>
        /// Ids of tasks that must complete first.
        /// </summary>
        [JsonPropertyName("dependsOn")]
        public List<string?>? DependsOn { get; set; } = null;
    }
}

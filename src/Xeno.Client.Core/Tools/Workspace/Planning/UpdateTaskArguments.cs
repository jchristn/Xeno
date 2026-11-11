namespace Xeno.Client.Tools.Workspace.Planning
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Arguments of update_task.
    /// </summary>
    public sealed class UpdateTaskArguments
    {
        /// <summary>
        /// Id of the task to update.
        /// </summary>
        [JsonPropertyName("id")]
        public string? Id { get; set; } = null;

        /// <summary>
        /// New status: pending, in_progress, completed, failed, skipped, or blocked.
        /// </summary>
        [JsonPropertyName("status")]
        public string? Status { get; set; } = null;

        /// <summary>
        /// Optional note; required when the status is failed.
        /// </summary>
        [JsonPropertyName("note")]
        public string? Note { get; set; } = null;
    }
}

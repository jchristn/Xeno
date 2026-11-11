namespace Xeno.Client.Tools.Workspace.Processes
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Result of process_list.
    /// </summary>
    public sealed class ProcessListResult
    {
        /// <summary>
        /// The conversation's processes, oldest first.
        /// </summary>
        [JsonPropertyName("processes")]
        public List<ProcessListItem> Processes { get; set; } = new List<ProcessListItem>();
    }
}

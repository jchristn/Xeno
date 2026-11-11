namespace Xeno.Client.Tools.Workspace.Planning
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Result of a successful update_task call.
    /// </summary>
    public sealed class UpdateTaskResult
    {
        /// <summary>
        /// Always true.
        /// </summary>
        [JsonPropertyName("ok")]
        public bool Ok { get; set; } = true;

        /// <summary>
        /// Task id.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Status as given.
        /// </summary>
        [JsonPropertyName("status")]
        public string Status { get; set; } = String.Empty;
    }
}

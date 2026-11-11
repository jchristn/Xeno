namespace Xeno.Client.Tools.Workspace.Processes
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// One process in process_list.
    /// </summary>
    public sealed class ProcessListItem
    {
        /// <summary>
        /// Process id.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Friendly name, or empty.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Command line.
        /// </summary>
        [JsonPropertyName("command")]
        public string Command { get; set; } = String.Empty;

        /// <summary>
        /// Full working directory.
        /// </summary>
        [JsonPropertyName("working_directory")]
        public string WorkingDirectory { get; set; } = String.Empty;

        /// <summary>
        /// Operating-system process id.
        /// </summary>
        [JsonPropertyName("pid")]
        public int Pid { get; set; } = 0;

        /// <summary>
        /// Whether the process is running.
        /// </summary>
        [JsonPropertyName("running")]
        public bool Running { get; set; } = false;

        /// <summary>
        /// Exit code, or null while running.
        /// </summary>
        [JsonPropertyName("exit_code")]
        public int? ExitCode { get; set; } = null;

        /// <summary>
        /// Whether Xeno stopped it.
        /// </summary>
        [JsonPropertyName("stopped_by_user")]
        public bool StoppedByUser { get; set; } = false;

        /// <summary>
        /// Start time, ISO 8601 UTC.
        /// </summary>
        [JsonPropertyName("started_utc")]
        public string StartedUtc { get; set; } = String.Empty;

        /// <summary>
        /// Characters of output not yet read.
        /// </summary>
        [JsonPropertyName("unread_chars")]
        public long UnreadChars { get; set; } = 0;
    }
}

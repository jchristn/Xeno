namespace Xeno.Client.Tools.Workspace.Processes
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Result of process_output.
    /// </summary>
    public sealed class ProcessOutputResult
    {
        /// <summary>
        /// Process id.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Whether the process is running.
        /// </summary>
        [JsonPropertyName("running")]
        public bool Running { get; set; } = false;

        /// <summary>
        /// Exit code once exited, or null while running.
        /// </summary>
        [JsonPropertyName("exit_code")]
        public int? ExitCode { get; set; } = null;

        /// <summary>
        /// Whether wait_for matched, or null without wait_for.
        /// </summary>
        [JsonPropertyName("matched")]
        public bool? Matched { get; set; } = null;

        /// <summary>
        /// Matching text, or null.
        /// </summary>
        [JsonPropertyName("match")]
        public string? Match { get; set; } = null;

        /// <summary>
        /// Whether the wait timed out.
        /// </summary>
        [JsonPropertyName("timed_out")]
        public bool TimedOut { get; set; } = false;

        /// <summary>
        /// Output since the previous read (the newest part when long).
        /// </summary>
        [JsonPropertyName("output")]
        public string Output { get; set; } = String.Empty;

        /// <summary>
        /// Unread characters dropped because the buffer was full.
        /// </summary>
        [JsonPropertyName("dropped_chars")]
        public long DroppedChars { get; set; } = 0;
    }
}

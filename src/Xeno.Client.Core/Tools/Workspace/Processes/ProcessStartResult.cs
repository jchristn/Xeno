namespace Xeno.Client.Tools.Workspace.Processes
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Result of process_start.
    /// </summary>
    public sealed class ProcessStartResult
    {
        /// <summary>
        /// Process id, for example p1.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Operating-system process id.
        /// </summary>
        [JsonPropertyName("pid")]
        public int Pid { get; set; } = 0;

        /// <summary>
        /// Command line as started.
        /// </summary>
        [JsonPropertyName("command")]
        public string Command { get; set; } = String.Empty;

        /// <summary>
        /// Full working directory.
        /// </summary>
        [JsonPropertyName("working_directory")]
        public string WorkingDirectory { get; set; } = String.Empty;

        /// <summary>
        /// Whether the process is running.
        /// </summary>
        [JsonPropertyName("running")]
        public bool Running { get; set; } = true;

        /// <summary>
        /// Exit code when the process already exited, or null.
        /// </summary>
        [JsonPropertyName("exit_code")]
        public int? ExitCode { get; set; } = null;

        /// <summary>
        /// With wait_for: whether the pattern appeared.
        /// </summary>
        [JsonPropertyName("ready")]
        public bool? Ready { get; set; } = null;

        /// <summary>
        /// With wait_for: the matching text.
        /// </summary>
        [JsonPropertyName("match")]
        public string? Match { get; set; } = null;

        /// <summary>
        /// With wait_for: whether the wait timed out (the process keeps running).
        /// </summary>
        [JsonPropertyName("timed_out")]
        public bool? TimedOut { get; set; } = null;

        /// <summary>
        /// With wait_for: output so far.
        /// </summary>
        [JsonPropertyName("output")]
        public string? Output { get; set; } = null;

        /// <summary>
        /// With wait_for: unread characters dropped because the buffer was full.
        /// </summary>
        [JsonPropertyName("dropped_chars")]
        public long? DroppedChars { get; set; } = null;

        /// <summary>
        /// Without wait_for: what to do next.
        /// </summary>
        [JsonPropertyName("next")]
        public string? Next { get; set; } = null;
    }
}

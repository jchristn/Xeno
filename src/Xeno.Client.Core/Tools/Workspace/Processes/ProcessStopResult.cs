namespace Xeno.Client.Tools.Workspace.Processes
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Result of process_stop: one process's final state and output, or with id "all" only the number stopped.
    /// </summary>
    public sealed class ProcessStopResult
    {
        /// <summary>
        /// Process id, or null for "all".
        /// </summary>
        [JsonPropertyName("id")]
        public string? Id { get; set; } = null;

        /// <summary>
        /// Whether the process was running before the call.
        /// </summary>
        [JsonPropertyName("was_running")]
        public bool? WasRunning { get; set; } = null;

        /// <summary>
        /// Whether the process is still running.
        /// </summary>
        [JsonPropertyName("running")]
        public bool? Running { get; set; } = null;

        /// <summary>
        /// Exit code, or null.
        /// </summary>
        [JsonPropertyName("exit_code")]
        public int? ExitCode { get; set; } = null;

        /// <summary>
        /// Last unread output.
        /// </summary>
        [JsonPropertyName("output")]
        public string? Output { get; set; } = null;

        /// <summary>
        /// Unread characters dropped because the buffer was full.
        /// </summary>
        [JsonPropertyName("dropped_chars")]
        public long? DroppedChars { get; set; } = null;

        /// <summary>
        /// With id "all": the number of processes stopped.
        /// </summary>
        [JsonPropertyName("stopped")]
        public int? Stopped { get; set; } = null;
    }
}

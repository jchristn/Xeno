namespace Xeno.Client.Tools.Workspace.Processes
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Result of run_process.
    /// </summary>
    public sealed class RunProcessResult
    {
        /// <summary>
        /// Standard output, truncated past the output limit.
        /// </summary>
        [JsonPropertyName("stdout")]
        public string Stdout { get; set; } = String.Empty;

        /// <summary>
        /// Standard error, truncated past the output limit.
        /// </summary>
        [JsonPropertyName("stderr")]
        public string Stderr { get; set; } = String.Empty;

        /// <summary>
        /// Exit code, or -1 when the process timed out.
        /// </summary>
        [JsonPropertyName("exit_code")]
        public int ExitCode { get; set; } = 0;

        /// <summary>
        /// True when the process ran past its timeout and was killed with its child processes.
        /// </summary>
        [JsonPropertyName("timed_out")]
        public bool TimedOut { get; set; } = false;
    }
}

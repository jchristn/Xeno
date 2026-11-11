namespace Xeno.Client.Tools.Workspace.Processes
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Arguments of process_start.
    /// </summary>
    public sealed class ProcessStartArguments
    {
        /// <summary>
        /// Program to run, or a command line when <see cref="Shell"/> is true.
        /// </summary>
        [JsonPropertyName("command")]
        public string? Command { get; set; } = null;

        /// <summary>
        /// Arguments passed to the program, or null.
        /// </summary>
        [JsonPropertyName("args")]
        public List<string>? Args { get; set; } = null;

        /// <summary>
        /// Run through the platform shell.
        /// </summary>
        [JsonPropertyName("shell")]
        public bool Shell { get; set; } = false;

        /// <summary>
        /// Working directory inside the conversation's working directory, or null for that directory.
        /// </summary>
        [JsonPropertyName("working_directory")]
        public string? WorkingDirectory { get; set; } = null;

        /// <summary>
        /// Optional short label.
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; } = null;

        /// <summary>
        /// Regular expression to wait for in the output, or null.
        /// </summary>
        [JsonPropertyName("wait_for")]
        public string? WaitFor { get; set; } = null;

        /// <summary>
        /// How long to wait for <see cref="WaitFor"/>, in milliseconds, or null for 30000.
        /// </summary>
        [JsonPropertyName("timeout_ms")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int? TimeoutMs { get; set; } = null;
    }
}

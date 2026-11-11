namespace Xeno.Client.Tools.Workspace.Processes
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Arguments of process_output.
    /// </summary>
    public sealed class ProcessOutputArguments
    {
        /// <summary>
        /// Process id from process_start.
        /// </summary>
        [JsonPropertyName("id")]
        public string? Id { get; set; } = null;

        /// <summary>
        /// Regular expression to wait for in the new output, or null.
        /// </summary>
        [JsonPropertyName("wait_for")]
        public string? WaitFor { get; set; } = null;

        /// <summary>
        /// How long to wait, in milliseconds, or null for 0 (30000 with <see cref="WaitFor"/>).
        /// </summary>
        [JsonPropertyName("timeout_ms")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int? TimeoutMs { get; set; } = null;
    }
}

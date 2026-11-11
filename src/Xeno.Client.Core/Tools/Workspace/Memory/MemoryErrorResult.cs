namespace Xeno.Client.Tools.Workspace.Memory
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Error returned to the model by remember, forget, or recall.
    /// </summary>
    public sealed class MemoryErrorResult
    {
        /// <summary>
        /// Error code: invalid_arguments, not_found, or memory_io_error.
        /// </summary>
        [JsonPropertyName("error")]
        public string Error { get; set; } = String.Empty;

        /// <summary>
        /// Explanation.
        /// </summary>
        [JsonPropertyName("message")]
        public string Message { get; set; } = String.Empty;
    }
}

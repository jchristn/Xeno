namespace Xeno.Client.Tools.Workspace.Files
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Result of manage_directory.
    /// </summary>
    public sealed class ManageDirectoryResult
    {
        /// <summary>
        /// True when the action succeeded.
        /// </summary>
        [JsonPropertyName("success")]
        public bool Success { get; set; } = true;

        /// <summary>
        /// Directory created or deleted, relative to the working directory.
        /// </summary>
        [JsonPropertyName("path")]
        public string? Path { get; set; } = null;

        /// <summary>
        /// Directory renamed from, relative to the working directory.
        /// </summary>
        [JsonPropertyName("from_path")]
        public string? FromPath { get; set; } = null;

        /// <summary>
        /// Directory renamed to, relative to the working directory.
        /// </summary>
        [JsonPropertyName("to_path")]
        public string? ToPath { get; set; } = null;

        /// <summary>
        /// Note about the outcome, for example that the directory already existed.
        /// </summary>
        [JsonPropertyName("message")]
        public string? Message { get; set; } = null;
    }
}

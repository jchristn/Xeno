namespace Xeno.Client.Tools.Workspace.Files
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Arguments of edit_file.
    /// </summary>
    public sealed class EditFileArguments
    {
        /// <summary>
        /// Path of the file to edit.
        /// </summary>
        [JsonPropertyName("file_path")]
        public string? FilePath { get; set; } = null;

        /// <summary>
        /// Exact text to find.
        /// </summary>
        [JsonPropertyName("old_string")]
        public string? OldString { get; set; } = null;

        /// <summary>
        /// Replacement text.
        /// </summary>
        [JsonPropertyName("new_string")]
        public string? NewString { get; set; } = null;

        /// <summary>
        /// True to replace every occurrence instead of requiring exactly one.
        /// </summary>
        [JsonPropertyName("replace_all")]
        public bool? ReplaceAll { get; set; } = null;
    }
}

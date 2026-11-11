namespace Xeno.Client.Tools.Workspace.Files
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Result of edit_file and multi_edit.
    /// </summary>
    public sealed class EditFileResult
    {
        /// <summary>
        /// True when the edits were applied.
        /// </summary>
        [JsonPropertyName("success")]
        public bool Success { get; set; } = true;

        /// <summary>
        /// Path edited, relative to the working directory.
        /// </summary>
        [JsonPropertyName("file_path")]
        public string FilePath { get; set; } = String.Empty;

        /// <summary>
        /// Replacements made.
        /// </summary>
        [JsonPropertyName("edits_applied")]
        public int EditsApplied { get; set; } = 0;

        /// <summary>
        /// Lines in the file after the edit.
        /// </summary>
        [JsonPropertyName("new_line_count")]
        public int NewLineCount { get; set; } = 0;
    }
}

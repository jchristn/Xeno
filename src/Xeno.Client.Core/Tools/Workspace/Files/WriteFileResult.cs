namespace Xeno.Client.Tools.Workspace.Files
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Result of write_file.
    /// </summary>
    public sealed class WriteFileResult
    {
        /// <summary>
        /// True when the file was written.
        /// </summary>
        [JsonPropertyName("success")]
        public bool Success { get; set; } = true;

        /// <summary>
        /// Path written, relative to the working directory.
        /// </summary>
        [JsonPropertyName("file_path")]
        public string FilePath { get; set; } = String.Empty;

        /// <summary>
        /// Lines written.
        /// </summary>
        [JsonPropertyName("line_count")]
        public int LineCount { get; set; } = 0;
    }
}

namespace Xeno.Client.Tools.Workspace.Files
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Result of delete_file.
    /// </summary>
    public sealed class DeleteFileResult
    {
        /// <summary>
        /// True when the file was deleted.
        /// </summary>
        [JsonPropertyName("success")]
        public bool Success { get; set; } = true;

        /// <summary>
        /// Path deleted, relative to the working directory.
        /// </summary>
        [JsonPropertyName("file_path")]
        public string FilePath { get; set; } = String.Empty;
    }
}

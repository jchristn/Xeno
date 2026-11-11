namespace Xeno.Client.Tools.Workspace.Files
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Arguments of write_file.
    /// </summary>
    public sealed class WriteFileArguments
    {
        /// <summary>
        /// Path of the file to write.
        /// </summary>
        [JsonPropertyName("file_path")]
        public string? FilePath { get; set; } = null;

        /// <summary>
        /// Content to write.
        /// </summary>
        [JsonPropertyName("content")]
        public string? Content { get; set; } = null;
    }
}

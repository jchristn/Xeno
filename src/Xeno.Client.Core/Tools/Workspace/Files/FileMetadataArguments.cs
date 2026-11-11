namespace Xeno.Client.Tools.Workspace.Files
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Arguments of file_metadata.
    /// </summary>
    public sealed class FileMetadataArguments
    {
        /// <summary>
        /// Path of the file or directory.
        /// </summary>
        [JsonPropertyName("path")]
        public string? Path { get; set; } = null;
    }
}

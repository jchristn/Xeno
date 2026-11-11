namespace Xeno.Client.Tools.Workspace.Files
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Arguments of delete_file.
    /// </summary>
    public sealed class DeleteFileArguments
    {
        /// <summary>
        /// Path of the file to delete.
        /// </summary>
        [JsonPropertyName("file_path")]
        public string? FilePath { get; set; } = null;
    }
}

namespace Xeno.Client.Tools.Workspace.Files
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Arguments of list_directory.
    /// </summary>
    public sealed class ListDirectoryArguments
    {
        /// <summary>
        /// Path of the directory to list.
        /// </summary>
        [JsonPropertyName("path")]
        public string? Path { get; set; } = null;
    }
}

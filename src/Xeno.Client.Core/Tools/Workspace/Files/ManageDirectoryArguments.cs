namespace Xeno.Client.Tools.Workspace.Files
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Arguments of manage_directory.
    /// </summary>
    public sealed class ManageDirectoryArguments
    {
        /// <summary>
        /// create, delete, or rename.
        /// </summary>
        [JsonPropertyName("action")]
        public string? Action { get; set; } = null;

        /// <summary>
        /// Directory to create, delete, or rename from.
        /// </summary>
        [JsonPropertyName("path")]
        public string? Path { get; set; } = null;

        /// <summary>
        /// New path for rename.
        /// </summary>
        [JsonPropertyName("new_path")]
        public string? NewPath { get; set; } = null;
    }
}

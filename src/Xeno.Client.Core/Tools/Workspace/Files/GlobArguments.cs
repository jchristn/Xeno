namespace Xeno.Client.Tools.Workspace.Files
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Arguments of glob.
    /// </summary>
    public sealed class GlobArguments
    {
        /// <summary>
        /// Glob pattern.
        /// </summary>
        [JsonPropertyName("pattern")]
        public string? Pattern { get; set; } = null;

        /// <summary>
        /// Directory to search; the working directory when empty.
        /// </summary>
        [JsonPropertyName("path")]
        public string? Path { get; set; } = null;
    }
}

namespace Xeno.Client.Tools.Workspace.Files
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Arguments of grep.
    /// </summary>
    public sealed class GrepArguments
    {
        /// <summary>
        /// Regular expression.
        /// </summary>
        [JsonPropertyName("pattern")]
        public string? Pattern { get; set; } = null;

        /// <summary>
        /// Directory to search; the working directory when empty.
        /// </summary>
        [JsonPropertyName("path")]
        public string? Path { get; set; } = null;

        /// <summary>
        /// Glob that limits which files are searched.
        /// </summary>
        [JsonPropertyName("include")]
        public string? Include { get; set; } = null;
    }
}

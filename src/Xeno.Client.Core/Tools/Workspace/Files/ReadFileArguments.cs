namespace Xeno.Client.Tools.Workspace.Files
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Arguments of read_file.
    /// </summary>
    public sealed class ReadFileArguments
    {
        /// <summary>
        /// Path of the file to read.
        /// </summary>
        [JsonPropertyName("file_path")]
        public string? FilePath { get; set; } = null;

        /// <summary>
        /// 1-based line to start at.
        /// </summary>
        [JsonPropertyName("offset")]
        public int? Offset { get; set; } = null;

        /// <summary>
        /// Most lines to read.
        /// </summary>
        [JsonPropertyName("limit")]
        public int? Limit { get; set; } = null;
    }
}

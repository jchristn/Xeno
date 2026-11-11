namespace Xeno.Client.Tools.Workspace.Files
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Arguments of multi_edit.
    /// </summary>
    public sealed class MultiEditArguments
    {
        /// <summary>
        /// Path of the file to edit.
        /// </summary>
        [JsonPropertyName("file_path")]
        public string? FilePath { get; set; } = null;

        /// <summary>
        /// Replacements applied in order.
        /// </summary>
        [JsonPropertyName("edits")]
        public List<MultiEditOperation>? Edits { get; set; } = null;
    }
}

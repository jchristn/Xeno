namespace Xeno.Client.Tools.Workspace.Files
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// One replacement in a multi_edit call.
    /// </summary>
    public sealed class MultiEditOperation
    {
        /// <summary>
        /// Exact text to find.
        /// </summary>
        [JsonPropertyName("old_string")]
        public string? OldString { get; set; } = null;

        /// <summary>
        /// Replacement text.
        /// </summary>
        [JsonPropertyName("new_string")]
        public string? NewString { get; set; } = null;
    }
}

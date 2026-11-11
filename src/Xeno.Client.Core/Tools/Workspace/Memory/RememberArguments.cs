namespace Xeno.Client.Tools.Workspace.Memory
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Arguments of remember.
    /// </summary>
    public sealed class RememberArguments
    {
        /// <summary>
        /// Short name, for example test-command.
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; } = null;

        /// <summary>
        /// One line shown in the memory index.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; } = null;

        /// <summary>
        /// Full fact; defaults to the description.
        /// </summary>
        [JsonPropertyName("content")]
        public string? Content { get; set; } = null;

        /// <summary>
        /// project (default) or global.
        /// </summary>
        [JsonPropertyName("scope")]
        public string? Scope { get; set; } = null;
    }
}

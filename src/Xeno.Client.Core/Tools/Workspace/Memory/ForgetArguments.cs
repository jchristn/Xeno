namespace Xeno.Client.Tools.Workspace.Memory
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Arguments of forget.
    /// </summary>
    public sealed class ForgetArguments
    {
        /// <summary>
        /// The memory's name.
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; } = null;

        /// <summary>
        /// project or global; omitted tries the project, then global.
        /// </summary>
        [JsonPropertyName("scope")]
        public string? Scope { get; set; } = null;
    }
}

namespace Xeno.Client.Tools.Workspace.Processes
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Arguments of process_stop.
    /// </summary>
    public sealed class ProcessStopArguments
    {
        /// <summary>
        /// Process id, or "all".
        /// </summary>
        [JsonPropertyName("id")]
        public string? Id { get; set; } = null;
    }
}

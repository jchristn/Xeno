namespace Xeno.Client.Tools.Workspace.Web
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// You.com result sections.
    /// </summary>
    internal sealed class YouApiResults
    {
        /// <summary>
        /// Web results.
        /// </summary>
        [JsonPropertyName("web")]
        public List<YouApiResult>? Web { get; set; } = null;

        /// <summary>
        /// News results.
        /// </summary>
        [JsonPropertyName("news")]
        public List<YouApiResult>? News { get; set; } = null;
    }
}

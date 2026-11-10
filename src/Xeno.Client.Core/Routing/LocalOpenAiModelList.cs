namespace Xeno.Client.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// OpenAI-compatible model list returned by GET /v1/models.
    /// </summary>
    public class LocalOpenAiModelList
    {
        /// <summary>
        /// Object type, always "list".
        /// </summary>
        [JsonPropertyName("object")]
        public string Object { get; set; } = "list";

        /// <summary>
        /// Models. Never null.
        /// </summary>
        [JsonPropertyName("data")]
        public List<LocalOpenAiModel> Data { get; set; } = new List<LocalOpenAiModel>();
    }
}

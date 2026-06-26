namespace Xeno.Core.Models
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Request body for bulk request history deletion.
    /// </summary>
    public class RequestHistoryDeleteRequest
    {
        /// <summary>
        /// Request history IDs to delete.
        /// </summary>
        [JsonPropertyName("ids")]
        public List<string>? Ids { get; set; } = null;

        /// <summary>
        /// Single request history ID to delete.
        /// </summary>
        [JsonPropertyName("id")]
        public string? Id { get; set; } = null;

        /// <summary>
        /// Optional model filter.
        /// </summary>
        [JsonPropertyName("model")]
        public string? Model { get; set; } = null;

        /// <summary>
        /// Optional request path filter.
        /// </summary>
        [JsonPropertyName("path")]
        public string? Path { get; set; } = null;

        /// <summary>
        /// Optional selected client filter.
        /// </summary>
        [JsonPropertyName("selectedClientId")]
        public string? SelectedClientId { get; set; } = null;
    }
}

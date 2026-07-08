namespace Xeno.Sdk
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Update counts produced by an administrative nuke operation.
    /// </summary>
    public class AdminNukeUpdatedCounts
    {
        /// <summary>
        /// Request history records that had user-identifying fields anonymized.
        /// </summary>
        [JsonPropertyName("requestHistoryAnonymized")]
        public int RequestHistoryAnonymized { get; set; } = 0;
    }
}

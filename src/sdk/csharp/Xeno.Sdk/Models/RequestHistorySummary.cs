namespace Xeno.Sdk
{
    using System.Collections.Generic;
    using System.Text.Json;

    /// <summary>
    /// Xeno request history summary DTO.
    /// </summary>
    public class RequestHistorySummary
    {
        /// <summary>
        /// Gets or sets the buckets.
        /// </summary>
        public List<JsonElement> Buckets { get; set; } = new List<JsonElement>();
    }
}

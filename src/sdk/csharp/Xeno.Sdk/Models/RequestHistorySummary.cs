namespace Xeno.Sdk
{
    using System.Collections.Generic;

    /// <summary>
    /// Xeno request history summary DTO.
    /// </summary>
    public class RequestHistorySummary
    {
        /// <summary>
        /// Gets or sets the buckets.
        /// </summary>
        public List<RawJsonValue> Buckets { get; set; } = new List<RawJsonValue>();
    }
}

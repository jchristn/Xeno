namespace Xeno.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Bucketed request counts returned by GET /v1.0/api/request-history/summary.
    /// </summary>
    public sealed class RequestHistorySummaryResponse
    {
        /// <summary>
        /// Range start, UTC.
        /// </summary>
        [JsonPropertyName("fromUtc")]
        public DateTime FromUtc { get; set; } = DateTime.MinValue;

        /// <summary>
        /// Range end, UTC.
        /// </summary>
        [JsonPropertyName("toUtc")]
        public DateTime ToUtc { get; set; } = DateTime.MinValue;

        /// <summary>
        /// Bucket width in minutes.
        /// </summary>
        [JsonPropertyName("bucketMinutes")]
        public int BucketMinutes { get; set; } = 15;

        /// <summary>
        /// Buckets in time order.
        /// </summary>
        [JsonPropertyName("buckets")]
        public List<RequestHistorySummaryBucketResponse> Buckets { get; set; } = new List<RequestHistorySummaryBucketResponse>();
    }
}

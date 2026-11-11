namespace Xeno.Core.Analytics
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Connected clients over a time window.
    /// </summary>
    public sealed class ConnectedClientHistory
    {
        #region Public-Members

        /// <summary>
        /// Window start.
        /// </summary>
        public DateTime FromUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Window end.
        /// </summary>
        public DateTime ToUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Bucket width in minutes.
        /// </summary>
        public int BucketMinutes { get; set; } = 1;

        /// <summary>
        /// Buckets, oldest first.
        /// </summary>
        public List<ConnectedClientHistoryBucket> Buckets { get; set; } = new List<ConnectedClientHistoryBucket>();

        #endregion
    }
}

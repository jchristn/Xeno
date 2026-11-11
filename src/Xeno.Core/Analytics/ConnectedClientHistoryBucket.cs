namespace Xeno.Core.Analytics
{
    using System;

    /// <summary>
    /// Connected clients in one bucket. Counts are null when no sample landed in the bucket, which means the server
    /// was not running then.
    /// </summary>
    public sealed class ConnectedClientHistoryBucket
    {
        #region Public-Members

        /// <summary>
        /// Bucket start.
        /// </summary>
        public DateTime StartUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Bucket end.
        /// </summary>
        public DateTime EndUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Clients connected at the bucket's last sample, or null with no sample.
        /// </summary>
        public int? Connected { get; set; } = null;

        /// <summary>
        /// Clients registered at the bucket's last sample, or null with no sample.
        /// </summary>
        public int? Total { get; set; } = null;

        #endregion
    }
}

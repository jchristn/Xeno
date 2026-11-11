namespace Xeno.Core.Analytics
{
    using System;

    /// <summary>
    /// Typed query for the provider health strip.
    /// </summary>
    public sealed class ProviderHealthQuery
    {
        private int _BucketMinutes = 15;
        private string? _ProviderName = null;
        private string? _ClientId = null;

        /// <summary>
        /// Range start, UTC.
        /// </summary>
        public DateTime FromUtc { get; set; } = DateTime.UtcNow.AddDays(-1);

        /// <summary>
        /// Range end, UTC.
        /// </summary>
        public DateTime ToUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Bucket size in minutes, 1 to 525,600. Default 15.
        /// </summary>
        public int BucketMinutes
        {
            get { return _BucketMinutes; }
            set { _BucketMinutes = Math.Clamp(value, 1, 525600); }
        }

        /// <summary>
        /// Only this provider, or null.
        /// </summary>
        public string? ProviderName
        {
            get { return _ProviderName; }
            set { _ProviderName = String.IsNullOrWhiteSpace(value) ? null : value.Trim(); }
        }

        /// <summary>
        /// Only this client (server), or null.
        /// </summary>
        public string? ClientId
        {
            get { return _ClientId; }
            set { _ClientId = String.IsNullOrWhiteSpace(value) ? null : value.Trim(); }
        }
    }
}

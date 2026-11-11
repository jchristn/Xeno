namespace Xeno.Core.Analytics
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// One provider health observation fed to the health strip builder.
    /// </summary>
    public sealed class ProviderHealthSample
    {
        /// <summary>
        /// When, UTC.
        /// </summary>
        public DateTime TimestampUtc { get; set; } = DateTime.MinValue;

        /// <summary>
        /// Provider.
        /// </summary>
        public string ProviderName { get; set; } = String.Empty;

        /// <summary>
        /// Client (server).
        /// </summary>
        public string? ClientId { get; set; } = null;

        /// <summary>
        /// True when the provider was healthy.
        /// </summary>
        public bool Healthy { get; set; } = true;
    }
}

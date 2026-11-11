namespace Xeno.Core.Analytics
{
    /// <summary>
    /// Health of a provider over one bucket.
    /// </summary>
    public enum ProviderHealthStatusEnum
    {
        /// <summary>
        /// Every sample in the bucket was healthy.
        /// </summary>
        Healthy,

        /// <summary>
        /// Some samples in the bucket failed.
        /// </summary>
        Degraded,

        /// <summary>
        /// Every sample in the bucket failed.
        /// </summary>
        Unhealthy,

        /// <summary>
        /// No samples in the bucket.
        /// </summary>
        NoData
    }
}

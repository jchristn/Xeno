namespace Xeno.Client.LocalDashboard.Database
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Local dashboard metadata database methods.
    /// </summary>
    public interface ILocalDashboardMetadataMethods
    {
        /// <summary>
        /// Set a metadata value.
        /// </summary>
        /// <param name="key">Metadata key.</param>
        /// <param name="value">Metadata value.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task SetMetadataAsync(string key, string value, CancellationToken token);

        /// <summary>
        /// Get a metadata value.
        /// </summary>
        /// <param name="key">Metadata key.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Metadata value if found.</returns>
        Task<string?> GetMetadataAsync(string key, CancellationToken token);
    }
}

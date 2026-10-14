namespace Xeno.Client.LocalDashboard.Database
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Local provider snapshot database methods.
    /// </summary>
    public interface ILocalProviderSnapshotMethods
    {
        /// <summary>
        /// Replace provider snapshots with the current configuration.
        /// </summary>
        /// <param name="providers">Configured providers.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task ReplaceProviderSnapshotsAsync(IReadOnlyList<ClientRunnerSettings> providers, CancellationToken token);
    }
}

namespace Xeno.Client.LocalDashboard.Database
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Local advertised model snapshot database methods.
    /// </summary>
    public interface ILocalModelSnapshotMethods
    {
        /// <summary>
        /// Replace model snapshots with the current configuration.
        /// </summary>
        /// <param name="providers">Configured providers.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task ReplaceModelSnapshotsAsync(IReadOnlyList<ClientRunnerSettings> providers, CancellationToken token);
    }
}

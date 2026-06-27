namespace Xeno.Client.LocalDashboard.Database
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Base local dashboard database driver.
    /// </summary>
    public abstract class LocalDashboardDatabaseDriverBase : IDisposable
    {
        /// <summary>
        /// Request history methods.
        /// </summary>
        public abstract ILocalRequestHistoryMethods RequestHistory { get; }

        /// <summary>
        /// Initialize database storage.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public abstract Task InitializeAsync(CancellationToken token);

        /// <summary>
        /// Dispose resources.
        /// </summary>
        public virtual void Dispose()
        {
        }
    }
}

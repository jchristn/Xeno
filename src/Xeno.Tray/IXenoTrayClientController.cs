namespace Xeno.Tray
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Controls and inspects the local Xeno client for the tray app.
    /// </summary>
    public interface IXenoTrayClientController
    {
        /// <summary>
        /// Read current status.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Status snapshot.</returns>
        Task<XenoTrayStatus> GetStatusAsync(CancellationToken token);

        /// <summary>
        /// Start the client if it is stopped.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Operation result.</returns>
        Task<XenoTrayOperationResult> StartAsync(CancellationToken token);

        /// <summary>
        /// Stop the client if it is running.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Operation result.</returns>
        Task<XenoTrayOperationResult> StopAsync(CancellationToken token);

        /// <summary>
        /// Read local request statistics.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Statistics snapshot.</returns>
        Task<XenoTrayStatistics> GetStatisticsAsync(CancellationToken token);

        /// <summary>
        /// Open the local dashboard if it is listening.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Operation result.</returns>
        Task<XenoTrayOperationResult> OpenDashboardAsync(CancellationToken token);
    }
}

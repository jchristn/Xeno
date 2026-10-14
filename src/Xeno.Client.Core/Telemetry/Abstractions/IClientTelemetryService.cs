namespace Xeno.Client.Telemetry.Abstractions
{
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Client.Telemetry.Models;

    /// <summary>
    /// Client telemetry collection service.
    /// </summary>
    public interface IClientTelemetryService
    {
        /// <summary>
        /// Collect one telemetry snapshot.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Telemetry snapshot.</returns>
        Task<ClientTelemetrySnapshot> CollectAsync(ClientSettings settings, CancellationToken token);
    }
}

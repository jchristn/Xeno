namespace Xeno.Client.Telemetry.Abstractions
{
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Client.Telemetry.Models;

    /// <summary>
    /// GPU telemetry provider.
    /// </summary>
    public interface IClientGpuTelemetryProvider
    {
        /// <summary>
        /// Collect GPU telemetry.
        /// </summary>
        /// <param name="settings">GPU settings.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>GPU telemetry.</returns>
        Task<ClientGpuTelemetry> CollectAsync(ClientGpuTelemetrySettings settings, CancellationToken token);
    }
}

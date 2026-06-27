namespace Xeno.Client.Telemetry.Abstractions
{
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Client.Telemetry.Models;

    /// <summary>
    /// Provider telemetry service.
    /// </summary>
    public interface IClientProviderTelemetryService
    {
        /// <summary>
        /// Collect provider telemetry.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Provider telemetry snapshot.</returns>
        Task<ClientProviderTelemetrySnapshot> CollectAsync(ClientSettings settings, CancellationToken token);
    }
}

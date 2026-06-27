namespace Xeno.Client.Telemetry.Abstractions
{
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Client.Telemetry.Models;

    /// <summary>
    /// System telemetry provider.
    /// </summary>
    public interface IClientSystemTelemetryProvider
    {
        /// <summary>
        /// Collect system telemetry.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>System telemetry sections.</returns>
        Task<ClientTelemetrySnapshot> CollectAsync(CancellationToken token);
    }
}

namespace Xeno.Client.Telemetry.Services
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Client.Telemetry.Abstractions;
    using Xeno.Client.Telemetry.Models;
    using Xeno.Core.Helpers;

    /// <summary>
    /// Default client telemetry service.
    /// </summary>
    public class ClientTelemetryService : IClientTelemetryService
    {
        private readonly IClientSystemTelemetryProvider _SystemProvider;
        private readonly IClientGpuTelemetryProvider _GpuProvider;

        /// <summary>
        /// Instantiate telemetry service.
        /// </summary>
        /// <param name="systemProvider">System provider.</param>
        /// <param name="gpuProvider">GPU provider.</param>
        public ClientTelemetryService(IClientSystemTelemetryProvider systemProvider, IClientGpuTelemetryProvider gpuProvider)
        {
            _SystemProvider = systemProvider ?? throw new ArgumentNullException(nameof(systemProvider));
            _GpuProvider = gpuProvider ?? throw new ArgumentNullException(nameof(gpuProvider));
        }

        /// <inheritdoc />
        public async Task<ClientTelemetrySnapshot> CollectAsync(ClientSettings settings, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(settings);
            token.ThrowIfCancellationRequested();
            Stopwatch stopwatch = Stopwatch.StartNew();
            ClientTelemetrySnapshot snapshot = await _SystemProvider.CollectAsync(token).ConfigureAwait(false);
            snapshot.Metadata.Id = IdGenerator.NewId("tel_");
            snapshot.Metadata.ClientId = settings.ClientId;
            snapshot.Metadata.MachineName = settings.Name;
            snapshot.Metadata.CollectedUtc = DateTime.UtcNow;
            snapshot.Gpu = await _GpuProvider.CollectAsync(settings.Telemetry.Gpu, token).ConfigureAwait(false);
            stopwatch.Stop();
            snapshot.Metadata.CollectionElapsedMs = Math.Round(stopwatch.Elapsed.TotalMilliseconds, 2);
            return snapshot;
        }
    }
}

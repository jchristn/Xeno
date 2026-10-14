namespace Xeno.Client.Telemetry.Services
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Client.LocalDashboard.Database;
    using Xeno.Client.Telemetry.Abstractions;
    using Xeno.Client.Telemetry.Models;

    /// <summary>
    /// Background telemetry persistence service.
    /// </summary>
    public sealed class ClientTelemetryPersistenceService : IDisposable
    {
        private readonly ClientSettings _Settings;
        private readonly IClientTelemetryHistoryMethods _History;
        private readonly IClientTelemetryService _Telemetry;
        private readonly CancellationTokenSource _TokenSource = new CancellationTokenSource();
        private readonly object _SyncRoot = new object();
        private Task? _Worker;
        private bool _Disposed;
        private int _CollectionRunning;

        /// <summary>
        /// Instantiate the persistence service.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="history">Telemetry history methods.</param>
        /// <param name="telemetry">Telemetry service.</param>
        public ClientTelemetryPersistenceService(ClientSettings settings, IClientTelemetryHistoryMethods history, IClientTelemetryService telemetry)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _History = history ?? throw new ArgumentNullException(nameof(history));
            _Telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
            Status = new ClientTelemetryPersistenceStatus
            {
                Enabled = _Settings.Telemetry.Enabled,
                CollectionIntervalMs = _Settings.Telemetry.CollectionIntervalMs,
                RetentionDays = _Settings.Telemetry.RetentionDays
            };
        }

        /// <summary>
        /// Last collected snapshot.
        /// </summary>
        public ClientTelemetrySnapshot? LatestSnapshot { get; private set; }

        /// <summary>
        /// Runtime status.
        /// </summary>
        public ClientTelemetryPersistenceStatus Status { get; private set; }

        /// <summary>
        /// Start telemetry collection.
        /// </summary>
        public void Start()
        {
            if (!_Settings.Telemetry.Enabled)
            {
                Status.Enabled = false;
                Status.Running = false;
                Status.LastMessage = "Telemetry is disabled.";
                return;
            }

            lock (_SyncRoot)
            {
                if (_Worker != null) return;
                Status.Enabled = true;
                Status.Running = true;
                Status.LastMessage = "Telemetry collection started.";
                _Worker = Task.Run(() => RunAsync(_TokenSource.Token));
            }
        }

        /// <summary>
        /// Trigger one immediate collection.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Snapshot.</returns>
        public async Task<ClientTelemetrySnapshot?> CollectNowAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return await CollectAndPersistAsync(token).ConfigureAwait(false);
        }

        /// <summary>
        /// Dispose the service.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private async Task RunAsync(CancellationToken token)
        {
            try
            {
                await CollectAndPersistSafeAsync(token).ConfigureAwait(false);
                await PruneSafeAsync(token).ConfigureAwait(false);
                DateTime nextPruneUtc = DateTime.UtcNow.AddMinutes(_Settings.Telemetry.PruneIntervalMinutes);
                using (PeriodicTimer collectionTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(_Settings.Telemetry.CollectionIntervalMs)))
                {
                    while (!token.IsCancellationRequested && await collectionTimer.WaitForNextTickAsync(token).ConfigureAwait(false))
                    {
                        await CollectAndPersistSafeAsync(token).ConfigureAwait(false);
                        if (DateTime.UtcNow >= nextPruneUtc)
                        {
                            await PruneSafeAsync(token).ConfigureAwait(false);
                            nextPruneUtc = DateTime.UtcNow.AddMinutes(_Settings.Telemetry.PruneIntervalMinutes);
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                Status.Running = false;
                Status.LastMessage = ex.Message;
                Console.Error.WriteLine(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.ffffff'Z'") + " [WARN] Telemetry worker stopped: " + ex.Message);
            }
        }

        private async Task CollectAndPersistSafeAsync(CancellationToken token)
        {
            try
            {
                await CollectAndPersistAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Status.LastMessage = ex.Message;
                Console.Error.WriteLine(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.ffffff'Z'") + " [WARN] Telemetry collection failed: " + ex.Message);
            }
        }

        private async Task<ClientTelemetrySnapshot?> CollectAndPersistAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (Interlocked.Exchange(ref _CollectionRunning, 1) == 1) return LatestSnapshot;
            try
            {
                ClientTelemetrySnapshot snapshot = await _Telemetry.CollectAsync(_Settings, token).ConfigureAwait(false);
                await _History.CreateAsync(snapshot, _Settings.Telemetry, token).ConfigureAwait(false);
                LatestSnapshot = snapshot;
                Status.LastCollectionUtc = snapshot.Metadata.CollectedUtc;
                Status.LastMessage = "Telemetry collected.";
                ClientTelemetryPersistenceStatus storedStatus = await _History.GetStatusAsync(_Settings.Telemetry, token).ConfigureAwait(false);
                storedStatus.Running = Status.Running;
                storedStatus.LastCollectionUtc = Status.LastCollectionUtc;
                storedStatus.LastPruneUtc = Status.LastPruneUtc;
                storedStatus.LastMessage = Status.LastMessage;
                Status = storedStatus;
                return snapshot;
            }
            finally
            {
                Interlocked.Exchange(ref _CollectionRunning, 0);
            }
        }

        private async Task PruneSafeAsync(CancellationToken token)
        {
            try
            {
                DateTime oldestRetainedUtc = DateTime.UtcNow.AddDays(-_Settings.Telemetry.RetentionDays);
                await _History.DeleteOlderThanAsync(oldestRetainedUtc, token).ConfigureAwait(false);
                Status.LastPruneUtc = DateTime.UtcNow;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Status.LastMessage = ex.Message;
                Console.Error.WriteLine(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.ffffff'Z'") + " [WARN] Telemetry pruning failed: " + ex.Message);
            }
        }

        private void Dispose(bool disposing)
        {
            if (_Disposed) return;
            if (disposing)
            {
                _TokenSource.Cancel();
                try
                {
                    _Worker?.Wait(TimeSpan.FromSeconds(5));
                }
                catch (AggregateException)
                {
                }

                _TokenSource.Dispose();
                Status.Running = false;
            }

            _Disposed = true;
        }
    }
}

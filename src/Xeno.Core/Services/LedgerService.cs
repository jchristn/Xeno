namespace Xeno.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Models;
    using Xeno.Core.Settings;

    /// <summary>
    /// Ledgering facade for request capture, queue draining, and status.
    /// </summary>
    public class LedgerService : IDisposable
    {
        #region Private-Members

        private readonly XenoPlatform _Platform;
        private readonly LedgerSettings _Settings;
        private readonly LedgerQueue _Queue;
        private readonly NetLedgerClient _Client;
        private readonly LedgerDrainService _DrainService;
        private readonly Func<CancellationToken, Task> _SavePlatformAsync;
        private readonly LedgerQueueStatus _Status = new LedgerQueueStatus();
        private CancellationTokenSource? _Cancellation;
        private Task? _DrainTask;
        private bool _Disposed;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the ledger service.
        /// </summary>
        /// <param name="platform">Platform state.</param>
        /// <param name="settings">Ledger settings.</param>
        /// <param name="savePlatformAsync">Platform persistence callback.</param>
        public LedgerService(XenoPlatform platform, LedgerSettings settings, Func<CancellationToken, Task> savePlatformAsync)
        {
            _Platform = platform ?? throw new ArgumentNullException(nameof(platform));
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _SavePlatformAsync = savePlatformAsync ?? throw new ArgumentNullException(nameof(savePlatformAsync));
            _Queue = new LedgerQueue(_Settings.MaxQueueDepth);
            _Client = new NetLedgerClient(_Settings);
            LedgerAccountResolver resolver = new LedgerAccountResolver(_Platform, _Settings, _Client);
            _DrainService = new LedgerDrainService(_Settings, _Client, resolver);
            _Status.Enabled = _Settings.Enabled;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Start the background drain loop.
        /// </summary>
        public void Start()
        {
            if (!_Settings.Enabled || _DrainTask != null) return;
            _Cancellation = new CancellationTokenSource();
            _DrainTask = Task.Run(async () => await DrainLoopAsync(_Cancellation.Token).ConfigureAwait(false));
        }

        /// <summary>
        /// Stop the background drain loop.
        /// </summary>
        public void Stop()
        {
            _Cancellation?.Cancel();
        }

        /// <summary>
        /// Try to enqueue ledger work from request history.
        /// </summary>
        /// <param name="history">Request history entry.</param>
        /// <param name="reason">Reason no event was queued.</param>
        /// <returns>True if an event was queued.</returns>
        public bool TryEnqueueFromHistory(RequestHistoryEntry history, out string? reason)
        {
            reason = null;
            if (!_Settings.Enabled)
            {
                reason = "Ledgering is disabled.";
                return false;
            }

            if (!LedgerEventFactory.TryCreate(_Platform, history, out LedgerEvent? ledgerEvent, out reason)) return false;
            if (ledgerEvent == null)
            {
                reason = "Ledger event could not be created.";
                return false;
            }

            if (!_Queue.TryEnqueue(ledgerEvent))
            {
                reason = "Ledger queue is full.";
                RecordFailure(reason, false);
                return false;
            }

            _Status.QueueDepth = _Queue.Count;
            return true;
        }

        /// <summary>
        /// Drain one batch immediately.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Drain response.</returns>
        public async Task<LedgerDrainResponse> DrainOnceAsync(CancellationToken token = default)
        {
            LedgerDrainResponse response = new LedgerDrainResponse();
            List<LedgerEvent> events = _Queue.DequeueBatch(_Settings.MaxBatchSize);
            foreach (LedgerEvent ledgerEvent in events)
            {
                token.ThrowIfCancellationRequested();
                if (ledgerEvent.NextAttemptUtc.HasValue && ledgerEvent.NextAttemptUtc.Value > DateTime.UtcNow)
                {
                    _Queue.Requeue(ledgerEvent);
                    continue;
                }

                try
                {
                    await _DrainService.ProcessAsync(ledgerEvent, token).ConfigureAwait(false);
                    response.Processed++;
                    _Status.ProcessedCount++;
                }
                catch (Exception ex) when (IsTerminal(ex))
                {
                    response.TerminalFailures++;
                    RecordFailure(ex.Message, true);
                }
                catch (Exception ex) when (ex is InvalidOperationException || ex is HttpRequestException || ex is TaskCanceledException)
                {
                    ledgerEvent.RetryCount++;
                    ledgerEvent.NextAttemptUtc = DateTime.UtcNow.AddMilliseconds(ComputeRetryDelayMs(ledgerEvent.RetryCount));
                    _Queue.Requeue(ledgerEvent);
                    response.Retried++;
                    _Status.RetryCount++;
                    RecordFailure(ex.Message, false);
                }
            }

            _Status.QueueDepth = _Queue.Count;
            _Status.OldestEventAgeMs = _Queue.GetOldestAgeMs();
            if (response.Processed > 0)
            {
                _Status.LastSuccessfulDrainUtc = DateTime.UtcNow;
                await _SavePlatformAsync(token).ConfigureAwait(false);
            }

            return response;
        }

        /// <summary>
        /// Build status response.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Status response.</returns>
        public async Task<LedgerStatusResponse> GetStatusAsync(CancellationToken token = default)
        {
            _Status.Enabled = _Settings.Enabled;
            _Status.QueueDepth = _Queue.Count;
            _Status.OldestEventAgeMs = _Queue.GetOldestAgeMs();
            LedgerStatusResponse response = new LedgerStatusResponse { Queue = _Status };
            if (!_Settings.Enabled) return response;

            try
            {
                response.NetLedgerHealthy = await _Client.HealthAsync(token).ConfigureAwait(false);
                if (!response.NetLedgerHealthy)
                {
                    response.NetLedgerMessage = "NetLedger health check returned an unsuccessful status.";
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is InvalidOperationException)
            {
                response.NetLedgerHealthy = false;
                response.NetLedgerMessage = ex.Message;
            }

            return response;
        }

        /// <summary>
        /// Dispose resources.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed) return;
            Stop();
            _Cancellation?.Dispose();
            _Client.Dispose();
            _Disposed = true;
        }

        #endregion

        #region Private-Methods

        private async Task DrainLoopAsync(CancellationToken token)
        {
            using (PeriodicTimer timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_Settings.DrainIntervalMs)))
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await timer.WaitForNextTickAsync(token).ConfigureAwait(false);
                        await DrainOnceAsync(token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        return;
                    }
                }
            }
        }

        private void RecordFailure(string? message, bool terminal)
        {
            _Status.LastFailureUtc = DateTime.UtcNow;
            _Status.LastFailure = message;
            if (terminal) _Status.TerminalFailureCount++;
        }

        private static bool IsTerminal(Exception ex)
        {
            return ex is ArgumentException || ex is KeyNotFoundException || ex is UnauthorizedAccessException;
        }

        private static int ComputeRetryDelayMs(int retryCount)
        {
            int bounded = Math.Clamp(retryCount, 1, 8);
            int baseDelay = Math.Min(60000, 500 * (int)Math.Pow(2, bounded - 1));
            int jitter = Random.Shared.Next(0, Math.Max(1, baseDelay / 5));
            return Math.Min(60000, baseDelay + jitter);
        }

        #endregion
    }
}

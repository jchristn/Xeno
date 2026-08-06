namespace Xeno.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using SyslogLogging;
    using Xeno.Core.Database;
    using Xeno.Core.Models;
    using Xeno.Core.Settings;

    /// <summary>
    /// Outcome of a ledger enqueue attempt.
    /// </summary>
    public class LedgerEnqueueResult
    {
        /// <summary>
        /// True when an event was queued.
        /// </summary>
        public bool Enqueued { get; set; }

        /// <summary>
        /// Reason no event was queued; otherwise null.
        /// </summary>
        public string? Reason { get; set; }
    }

    /// <summary>
    /// Ledgering facade for request capture, queue draining, and status.
    /// </summary>
    public class LedgerService : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Current ledger queue depth.
        /// </summary>
        public int QueueDepth
        {
            get => _Queue.Count;
        }

        #endregion

        #region Private-Members

        private readonly DatabaseDriverBase _Database;
        private readonly LedgerSettings _Settings;
        private readonly LoggingModule _Logging;
        private readonly string _Header = "[LedgerService] ";
        private readonly LedgerQueue _Queue;
        private readonly NetLedgerClient _Client;
        private readonly LedgerDrainService _DrainService;
        private readonly LedgerQueueStatus _Status = new LedgerQueueStatus();
        private CancellationTokenSource? _Cancellation;
        private Task? _DrainTask;
        private bool _Disposed;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the ledger service.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="settings">Ledger settings.</param>
        /// <param name="logging">Logging module.</param>
        public LedgerService(DatabaseDriverBase database, LedgerSettings settings, LoggingModule logging)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _Queue = new LedgerQueue(_Settings.MaxQueueDepth);
            _Client = new NetLedgerClient(_Settings, _Logging);
            LedgerAccountResolver resolver = new LedgerAccountResolver(_Database, _Settings, _Client);
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
            if (!_Settings.Enabled)
            {
                _Logging.Debug(_Header + "ledgering disabled, not starting");
                return;
            }

            if (_DrainTask != null) return;
            _Logging.Debug(_Header + "starting");
            _Cancellation = new CancellationTokenSource();
            _DrainTask = Task.Run(async () => await DrainLoopAsync(_Cancellation.Token).ConfigureAwait(false));
        }

        /// <summary>
        /// Stop the background drain loop.
        /// </summary>
        public void Stop()
        {
            _Logging.Debug(_Header + "stopping");
            _Cancellation?.Cancel();
        }

        /// <summary>
        /// Try to enqueue ledger work from request history.
        /// </summary>
        /// <param name="history">Request history entry.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Ledger enqueue result.</returns>
        public async Task<LedgerEnqueueResult> TryEnqueueFromHistoryAsync(RequestHistoryEntry history, CancellationToken token = default)
        {
            if (!_Settings.Enabled)
            {
                return new LedgerEnqueueResult { Enqueued = false, Reason = "Ledgering is disabled." };
            }

            LedgerEventCreationResult creation = await LedgerEventFactory.TryCreateAsync(_Database, history, token).ConfigureAwait(false);
            if (!creation.Success)
            {
                return new LedgerEnqueueResult { Enqueued = false, Reason = creation.Reason };
            }

            if (creation.Event == null)
            {
                return new LedgerEnqueueResult { Enqueued = false, Reason = "Ledger event could not be created." };
            }

            if (!_Queue.TryEnqueue(creation.Event))
            {
                string reason = "Ledger queue is full.";
                RecordFailure(reason, false);
                return new LedgerEnqueueResult { Enqueued = false, Reason = reason };
            }

            _Status.QueueDepth = _Queue.Count;
            return new LedgerEnqueueResult { Enqueued = true, Reason = null };
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
                    _Logging.Warn(_Header + "dropping ledger event " + ledgerEvent.Id + " after terminal failure: " + ex.Message);
                }
                catch (Exception ex) when (ex is InvalidOperationException || ex is HttpRequestException || ex is TaskCanceledException)
                {
                    ledgerEvent.RetryCount++;
                    ledgerEvent.NextAttemptUtc = DateTime.UtcNow.AddMilliseconds(ComputeRetryDelayMs(ledgerEvent.RetryCount));
                    _Queue.Requeue(ledgerEvent);
                    response.Retried++;
                    _Status.RetryCount++;
                    _Logging.Warn(_Header + "retrying ledger event " + ledgerEvent.Id + " (attempt " + ledgerEvent.RetryCount + "): " + ex.Message);
                    RecordFailure(ex.Message, false);
                }
            }

            _Status.QueueDepth = _Queue.Count;
            _Status.OldestEventAgeMs = _Queue.GetOldestAgeMs();
            if (response.Processed > 0)
            {
                _Status.LastSuccessfulDrainUtc = DateTime.UtcNow;
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
            _Logging.Debug(_Header + "drain loop starting");
            try
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
                            break;
                        }
                    }
                }
            }
            finally
            {
                _Logging.Debug(_Header + "drain loop stopping");
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

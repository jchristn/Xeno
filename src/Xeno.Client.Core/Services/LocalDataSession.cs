namespace Xeno.Client.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Client.LocalDashboard.Database;
    using Xeno.Client.LocalDashboard.Models;
    using Xeno.Client.Telemetry.Abstractions;
    using Xeno.Client.Telemetry.Models;
    using Xeno.Client.Telemetry.Providers;
    using Xeno.Client.Telemetry.Services;

    /// <summary>
    /// Opens the local history database (request history and telemetry) for front ends that run outside the worker:
    /// the desktop app, the terminal app, and the tray agent. The worker keeps writing while a session is open.
    /// Dispose when finished. Not thread-safe for concurrent initialization; queries may run concurrently.
    /// </summary>
    public sealed class LocalDataSession : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Request history.
        /// </summary>
        public ILocalRequestHistoryMethods RequestHistory
        {
            get { return Driver.RequestHistory; }
        }

        /// <summary>
        /// Host telemetry history.
        /// </summary>
        public IClientTelemetryHistoryMethods TelemetryHistory
        {
            get { return Driver.TelemetryHistory; }
        }

        /// <summary>
        /// Provider telemetry history.
        /// </summary>
        public IClientProviderTelemetryHistoryMethods ProviderTelemetryHistory
        {
            get { return Driver.ProviderTelemetryHistory; }
        }

        /// <summary>
        /// Settings the session was opened with.
        /// </summary>
        public ClientSettings Settings { get; }

        #endregion

        #region Private-Members

        private readonly LocalDashboardDatabaseDriverBase _Driver;
        private bool _Initialized = false;
        private bool _Disposed = false;

        private LocalDashboardDatabaseDriverBase Driver
        {
            get
            {
                if (!_Initialized) throw new InvalidOperationException("Call OpenAsync before using the local data session.");
                return _Driver;
            }
        }

        #endregion

        #region Constructors-and-Factories

        private LocalDataSession(ClientSettings settings)
        {
            Settings = settings;
            _Driver = LocalDashboardDatabaseDriverFactory.Create(settings.LocalDashboard.Database);
        }

        /// <summary>
        /// Open the local history database configured in the settings, creating its tables when missing.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Open session.</returns>
        /// <exception cref="ArgumentNullException">Settings is null.</exception>
        public static async Task<LocalDataSession> OpenAsync(ClientSettings settings, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(settings);
            LocalDataSession session = new LocalDataSession(settings);
            try
            {
                await session._Driver.InitializeAsync(token).ConfigureAwait(false);
                session._Initialized = true;
                return session;
            }
            catch
            {
                session.Dispose();
                throw;
            }
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Describe the database file: location, size, and request counts.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Database description.</returns>
        public async Task<LocalDashboardDatabaseResponse> DescribeDatabaseAsync(CancellationToken token)
        {
            string resolvedFilename = Settings.LocalDashboard.Database.ResolveFilename();
            FileInfo file = new FileInfo(resolvedFilename);
            LocalDashboardStatisticsResponse statistics = await RequestHistory.StatisticsAsync(token).ConfigureAwait(false);
            return new LocalDashboardDatabaseResponse
            {
                Status = file.Exists ? LocalDashboardDatabaseStatusEnum.Ready : LocalDashboardDatabaseStatusEnum.Missing,
                Filename = Settings.LocalDashboard.Database.Filename,
                ResolvedFilename = resolvedFilename,
                Exists = file.Exists,
                SizeBytes = file.Exists ? file.Length : 0,
                Requests = statistics.Requests,
                OldestRequestUtc = statistics.OldestRequestUtc,
                NewestRequestUtc = statistics.NewestRequestUtc
            };
        }

        /// <summary>
        /// Summarize request history into time buckets for a range.
        /// </summary>
        /// <param name="range">Range.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Buckets, oldest first.</returns>
        public async Task<List<LocalRequestHistorySummaryBucket>> SummarizeAsync(HistoryRangeEnum range, CancellationToken token)
        {
            DateTime toUtc = DateTime.UtcNow;
            IReadOnlyList<LocalRequestHistorySummaryBucket> buckets = await RequestHistory
                .SummarizeAsync(HistoryRanges.StartUtc(range, toUtc), toUtc, HistoryRanges.DefaultBucketMinutes(range), token)
                .ConfigureAwait(false);
            return buckets.ToList();
        }

        /// <summary>
        /// Collect and store one host telemetry sample now (CPU, memory, disk, GPU).
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Snapshot, or null when collection failed.</returns>
        public async Task<ClientTelemetrySnapshot?> CollectHostTelemetryAsync(CancellationToken token)
        {
            ClientTelemetryCommandRunner runner = new ClientTelemetryCommandRunner();
            IClientTelemetryService telemetry = new ClientTelemetryService(new ClientSystemTelemetryProvider(runner), new ClientGpuTelemetryProvider(runner));
            using (ClientTelemetryPersistenceService service = new ClientTelemetryPersistenceService(Settings, TelemetryHistory, telemetry))
            {
                return await service.CollectNowAsync(token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Collect and store one provider telemetry sample now for every configured provider.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Snapshot, or null when collection failed.</returns>
        public async Task<ClientProviderTelemetrySnapshot?> CollectProviderTelemetryAsync(CancellationToken token)
        {
            using (ClientProviderTelemetryPersistenceService service = new ClientProviderTelemetryPersistenceService(Settings, ProviderTelemetryHistory, new ClientProviderTelemetryService(new PrometheusMetricsParser())))
            {
                return await service.CollectNowAsync(token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Dispose the session and close the database.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;
            _Driver.Dispose();
        }

        #endregion
    }
}

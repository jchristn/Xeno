namespace Xeno.Client.LocalDashboard.Database.Sqlite
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// SQLite local dashboard database driver.
    /// </summary>
    public sealed class SqliteLocalDashboardDatabaseDriver : LocalDashboardDatabaseDriverBase
    {
        private readonly SqliteLocalRequestHistoryMethods _RequestHistory;
        private readonly SqliteClientTelemetryHistoryMethods _TelemetryHistory;
        private readonly SqliteClientProviderTelemetryHistoryMethods _ProviderTelemetryHistory;

        /// <summary>
        /// Instantiate the SQLite local dashboard database driver.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        public SqliteLocalDashboardDatabaseDriver(ClientLocalDatabaseSettings settings)
        {
            _RequestHistory = new SqliteLocalRequestHistoryMethods(settings);
            _TelemetryHistory = new SqliteClientTelemetryHistoryMethods(settings);
            _ProviderTelemetryHistory = new SqliteClientProviderTelemetryHistoryMethods(settings);
        }

        /// <inheritdoc />
        public override ILocalRequestHistoryMethods RequestHistory
        {
            get
            {
                return _RequestHistory;
            }
        }

        /// <inheritdoc />
        public override IClientTelemetryHistoryMethods TelemetryHistory
        {
            get
            {
                return _TelemetryHistory;
            }
        }

        /// <inheritdoc />
        public override IClientProviderTelemetryHistoryMethods ProviderTelemetryHistory
        {
            get
            {
                return _ProviderTelemetryHistory;
            }
        }

        /// <inheritdoc />
        public override async Task InitializeAsync(CancellationToken token)
        {
            await _RequestHistory.InitializeAsync(token).ConfigureAwait(false);
            await _TelemetryHistory.InitializeAsync(token).ConfigureAwait(false);
            await _ProviderTelemetryHistory.InitializeAsync(token).ConfigureAwait(false);
        }
    }
}

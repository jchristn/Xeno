namespace Xeno.Core.Database.Postgresql
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using SyslogLogging;
    using Npgsql;
    using Xeno.Core.Database;
    using Xeno.Core.Enums;
    using Xeno.Core.Settings;

    /// <summary>
    /// PostgreSQL database driver.
    /// </summary>
    public class PostgresqlDatabaseDriver : DatabaseDriverBase
    {
        private const string StateId = "platform";
        private string _ConnectionString = String.Empty;

        /// <summary>
        /// Instantiate the PostgreSQL database driver.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        /// <param name="logging">Optional logging module.</param>
        public PostgresqlDatabaseDriver(DatabaseSettings settings, LoggingModule? logging = null) : base(settings, logging)
        {
        }

        /// <summary>
        /// Database provider type.
        /// </summary>
        public override DatabaseTypeEnum DatabaseType { get { return DatabaseTypeEnum.Postgresql; } }

        /// <inheritdoc />
        protected override async Task<bool> InitializeStorageAsync(CancellationToken token = default)
        {
            if (!TryBuildConnectionString(out _ConnectionString)) return false;

            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (NpgsqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = """
CREATE TABLE IF NOT EXISTS schema_migrations
(
    id varchar(64) PRIMARY KEY,
    name varchar(256) NOT NULL,
    applied_utc timestamptz NOT NULL
);

CREATE TABLE IF NOT EXISTS xeno_platform_state
(
    id varchar(64) PRIMARY KEY,
    data text NOT NULL,
    created_utc timestamptz NOT NULL,
    updated_utc timestamptz NOT NULL,
    active boolean NOT NULL DEFAULT true
);

INSERT INTO schema_migrations (id, name, applied_utc)
VALUES ('0001', 'initial_platform_state', now())
ON CONFLICT (id) DO NOTHING;
""";
                    await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }

                await PostgresqlTelemetryMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await PostgresqlRequestHistoryMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await PostgresqlAuditMethods.InitializeAsync(connection, token).ConfigureAwait(false);
            }

            PostgresqlTelemetryMethods telemetryMethods = new PostgresqlTelemetryMethods(_ConnectionString);
            ClientTelemetry = telemetryMethods;
            ClientProviderTelemetry = telemetryMethods;
            ClientTelemetryCollectionAttempts = telemetryMethods;
            RequestHistory = new PostgresqlRequestHistoryMethods(_ConnectionString);
            Audit = new PostgresqlAuditMethods(_ConnectionString);
            return true;
        }

        /// <inheritdoc />
        protected override async Task<string?> LoadStateJsonAsync(CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(_ConnectionString)) return null;
            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (NpgsqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT data FROM xeno_platform_state WHERE id = @id AND active = true;";
                    command.Parameters.AddWithValue("@id", StateId);
                    object? value = await command.ExecuteScalarAsync(token).ConfigureAwait(false);
                    return value?.ToString();
                }
            }
        }

        /// <inheritdoc />
        protected override async Task SaveStateJsonAsync(string stateJson, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(_ConnectionString)) return;
            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (NpgsqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = """
INSERT INTO xeno_platform_state (id, data, created_utc, updated_utc, active)
VALUES (@id, @data, now(), now(), true)
ON CONFLICT (id) DO UPDATE SET
    data = excluded.data,
    updated_utc = now(),
    active = true;
""";
                    command.Parameters.AddWithValue("@id", StateId);
                    command.Parameters.AddWithValue("@data", stateJson);
                    await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }
        }

        /// <inheritdoc />
        public override async Task<int> PurgeTenantDataAsync(string tenantId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
            token.ThrowIfCancellationRequested();
            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                return await PostgresqlTelemetryMethods.PurgeTenantAsync(connection, tenantId, token).ConfigureAwait(false);
            }
        }

        private bool TryBuildConnectionString(out string connectionString)
        {
            if (!String.IsNullOrWhiteSpace(Settings.ConnectionString))
            {
                connectionString = Settings.ConnectionString;
                return true;
            }

            if (String.IsNullOrWhiteSpace(Settings.Server) || String.IsNullOrWhiteSpace(Settings.DatabaseName))
            {
                connectionString = String.Empty;
                return false;
            }

            NpgsqlConnectionStringBuilder builder = new NpgsqlConnectionStringBuilder
            {
                Host = Settings.Server,
                Port = Settings.Port > 0 ? Settings.Port : 5432,
                Database = Settings.DatabaseName,
                Username = Settings.Username ?? String.Empty,
                Password = Settings.Password ?? String.Empty
            };
            connectionString = builder.ConnectionString;
            return true;
        }
    }
}

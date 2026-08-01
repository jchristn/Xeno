namespace Xeno.Core.Database.SqlServer
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using SyslogLogging;
    using Microsoft.Data.SqlClient;
    using Xeno.Core.Database;
    using Xeno.Core.Enums;
    using Xeno.Core.Settings;

    /// <summary>
    /// SQL Server database driver.
    /// </summary>
    public class SqlServerDatabaseDriver : DatabaseDriverBase
    {
        private const string StateId = "platform";
        private string _ConnectionString = String.Empty;

        /// <summary>
        /// Instantiate the SQL Server database driver.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        /// <param name="logging">Optional logging module.</param>
        public SqlServerDatabaseDriver(DatabaseSettings settings, LoggingModule? logging = null) : base(settings, logging)
        {
        }

        /// <summary>
        /// Database provider type.
        /// </summary>
        public override DatabaseTypeEnum DatabaseType { get { return DatabaseTypeEnum.SqlServer; } }

        /// <inheritdoc />
        protected override async Task<bool> InitializeStorageAsync(CancellationToken token = default)
        {
            if (!TryBuildConnectionString(out _ConnectionString)) return false;

            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = """
IF OBJECT_ID(N'dbo.schema_migrations', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.schema_migrations
    (
        id nvarchar(64) NOT NULL PRIMARY KEY,
        name nvarchar(256) NOT NULL,
        applied_utc datetime2 NOT NULL
    );
END;

IF OBJECT_ID(N'dbo.xeno_platform_state', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.xeno_platform_state
    (
        id nvarchar(64) NOT NULL PRIMARY KEY,
        data nvarchar(max) NOT NULL,
        created_utc datetime2 NOT NULL,
        updated_utc datetime2 NOT NULL,
        active bit NOT NULL DEFAULT 1
    );
END;

IF NOT EXISTS (SELECT 1 FROM dbo.schema_migrations WHERE id = N'0001')
BEGIN
    INSERT INTO dbo.schema_migrations (id, name, applied_utc)
    VALUES (N'0001', N'initial_platform_state', SYSUTCDATETIME());
END;
""";
                    await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }

                await SqlServerTelemetryMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await SqlServerRequestHistoryMethods.InitializeAsync(connection, token).ConfigureAwait(false);
            }

            SqlServerTelemetryMethods telemetryMethods = new SqlServerTelemetryMethods(_ConnectionString);
            ClientTelemetry = telemetryMethods;
            ClientProviderTelemetry = telemetryMethods;
            ClientTelemetryCollectionAttempts = telemetryMethods;
            RequestHistory = new SqlServerRequestHistoryMethods(_ConnectionString);
            return true;
        }

        /// <inheritdoc />
        protected override async Task<string?> LoadStateJsonAsync(CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(_ConnectionString)) return null;
            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT data FROM dbo.xeno_platform_state WHERE id = @id AND active = 1;";
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
            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = """
IF EXISTS (SELECT 1 FROM dbo.xeno_platform_state WHERE id = @id)
BEGIN
    UPDATE dbo.xeno_platform_state
    SET data = @data, updated_utc = SYSUTCDATETIME(), active = 1
    WHERE id = @id;
END
ELSE
BEGIN
    INSERT INTO dbo.xeno_platform_state (id, data, created_utc, updated_utc, active)
    VALUES (@id, @data, SYSUTCDATETIME(), SYSUTCDATETIME(), 1);
END;
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
            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                return await SqlServerTelemetryMethods.PurgeTenantAsync(connection, tenantId, token).ConfigureAwait(false);
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

            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder
            {
                DataSource = Settings.Port > 0 ? Settings.Server + "," + Settings.Port : Settings.Server,
                InitialCatalog = Settings.DatabaseName,
                UserID = Settings.Username ?? String.Empty,
                Password = Settings.Password ?? String.Empty,
                TrustServerCertificate = true
            };
            connectionString = builder.ConnectionString;
            return true;
        }
    }
}

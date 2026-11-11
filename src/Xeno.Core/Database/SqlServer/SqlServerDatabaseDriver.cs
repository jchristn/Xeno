namespace Xeno.Core.Database.SqlServer
{
    using System;
    using System.Collections.Generic;
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
                await SqlServerAuditMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await SqlServerOnboardingMethods.InitializeAsync(connection, token).ConfigureAwait(false);

                await SqlServerTenantMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await SqlServerUserMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await SqlServerCredentialMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await SqlServerAuthSessionMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await SqlServerRoleMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await SqlServerPermissionMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await SqlServerRoleAssignmentMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await SqlServerClientMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await SqlServerModelDefinitionMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await SqlServerModelConfigurationMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await SqlServerRoutingPolicyMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await SqlServerLedgerTenantLinkMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await SqlServerLedgerAccountLinkMethods.InitializeAsync(connection, token).ConfigureAwait(false);

                List<SchemaMigration> forwardMigrations = new List<SchemaMigration>
                {
                    // Post-baseline schema changes are applied in ascending version order and recorded in
                    // schema_migrations; add new SchemaMigration entries here as the schema evolves.
                };
                await SchemaMigrationRunner.ApplyAsync(connection, "SYSUTCDATETIME()", forwardMigrations, token).ConfigureAwait(false);
            }

            SqlServerTelemetryMethods telemetryMethods = new SqlServerTelemetryMethods(_ConnectionString);
            ClientTelemetry = telemetryMethods;
            ClientProviderTelemetry = telemetryMethods;
            ClientTelemetryCollectionAttempts = telemetryMethods;
            RequestHistory = new SqlServerRequestHistoryMethods(_ConnectionString);
            Audit = new SqlServerAuditMethods(_ConnectionString);
            string chatConnectionString = _ConnectionString;
            DbChatMethods chat = new DbChatMethods(ChatSqlDialectEnum.SqlServer, () => new SqlConnection(chatConnectionString));
            await chat.InitializeAsync(token).ConfigureAwait(false);
            Chat = chat;
            DbConnectedClientSampleMethods connectedClientSamples = new DbConnectedClientSampleMethods(ChatSqlDialectEnum.SqlServer, () => new SqlConnection(chatConnectionString));
            await connectedClientSamples.InitializeAsync(token).ConfigureAwait(false);
            ConnectedClientSamples = connectedClientSamples;
            Onboarding = new SqlServerOnboardingMethods(_ConnectionString);

            Tenants = new SqlServerTenantMethods(_ConnectionString);
            Users = new SqlServerUserMethods(_ConnectionString);
            Credentials = new SqlServerCredentialMethods(_ConnectionString);
            AuthSessions = new SqlServerAuthSessionMethods(_ConnectionString);
            Roles = new SqlServerRoleMethods(_ConnectionString);
            Permissions = new SqlServerPermissionMethods(_ConnectionString);
            RoleAssignments = new SqlServerRoleAssignmentMethods(_ConnectionString);
            Clients = new SqlServerClientMethods(_ConnectionString);
            ModelDefinitions = new SqlServerModelDefinitionMethods(_ConnectionString);
            ModelConfigurations = new SqlServerModelConfigurationMethods(_ConnectionString);
            RoutingPolicies = new SqlServerRoutingPolicyMethods(_ConnectionString);
            LedgerTenantLinks = new SqlServerLedgerTenantLinkMethods(_ConnectionString);
            LedgerAccountLinks = new SqlServerLedgerAccountLinkMethods(_ConnectionString);
            return true;
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

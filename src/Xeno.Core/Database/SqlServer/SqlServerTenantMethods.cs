namespace Xeno.Core.Database.SqlServer
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.SqlClient;
    using Xeno.Core.Database.Interfaces;
    using Xeno.Core.Helpers;
    using Xeno.Core.Models;

    /// <summary>
    /// SQL Server tenant data access methods. Tenant scalar fields map to columns on <c>dbo.tenants</c>
    /// and the <see cref="Tenant.SubordinateServices"/> collection maps to the ordered child table
    /// <c>dbo.tenant_subordinate_services</c>.
    /// </summary>
    public class SqlServerTenantMethods : ITenantMethods
    {
        #region Private-Members

        private readonly string _ConnectionString;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="connectionString">Connection string.</param>
        public SqlServerTenantMethods(string connectionString)
        {
            if (String.IsNullOrEmpty(connectionString)) throw new ArgumentNullException(nameof(connectionString));
            _ConnectionString = connectionString;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Create the tenant tables and indexes if they do not exist.
        /// </summary>
        /// <param name="connection">Open connection.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public static async Task InitializeAsync(SqlConnection connection, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(connection);
            token.ThrowIfCancellationRequested();
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = SchemaSql();
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public async Task<Tenant> CreateAsync(Tenant tenant, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(tenant);
            token.ThrowIfCancellationRequested();
            if (String.IsNullOrWhiteSpace(tenant.Id)) tenant.Id = IdGenerator.NewId("ten_");

            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    await UpsertTenantRowAsync(connection, transaction, tenant, token).ConfigureAwait(false);
                    await ReplaceSubordinateServicesAsync(connection, transaction, tenant, token).ConfigureAwait(false);
                    await transaction.CommitAsync(token).ConfigureAwait(false);
                }
            }

            return tenant;
        }

        /// <inheritdoc />
        public async Task<Tenant?> ReadAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) return null;
            token.ThrowIfCancellationRequested();
            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                Tenant? tenant = null;
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT TOP (1) " + TenantColumns + " FROM dbo.tenants WHERE id = @id;";
                    command.Parameters.AddWithValue("@id", id);
                    using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        if (await reader.ReadAsync(token).ConfigureAwait(false)) tenant = HydrateTenant(reader);
                    }
                }

                if (tenant == null) return null;
                tenant.SubordinateServices = await ReadSubordinateServicesAsync(connection, id, token).ConfigureAwait(false);
                return tenant;
            }
        }

        /// <inheritdoc />
        public async Task<List<Tenant>> EnumerateAsync(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            List<Tenant> tenants = new List<Tenant>();
            Dictionary<string, Tenant> byId = new Dictionary<string, Tenant>(StringComparer.Ordinal);

            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + TenantColumns + " FROM dbo.tenants ORDER BY created_utc ASC, id ASC;";
                    using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(token).ConfigureAwait(false))
                        {
                            Tenant tenant = HydrateTenant(reader);
                            tenants.Add(tenant);
                            byId[tenant.Id] = tenant;
                        }
                    }
                }

                if (tenants.Count > 0)
                {
                    using (SqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT " + SubordinateColumns + " FROM dbo.tenant_subordinate_services ORDER BY tenant_id ASC, ordinal ASC;";
                        using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                        {
                            while (await reader.ReadAsync(token).ConfigureAwait(false))
                            {
                                string tenantId = reader.GetString(0);
                                if (byId.TryGetValue(tenantId, out Tenant? owner)) owner.SubordinateServices.Add(HydrateSubordinate(reader));
                            }
                        }
                    }
                }
            }

            return tenants;
        }

        /// <inheritdoc />
        public async Task<Tenant> UpdateAsync(Tenant tenant, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(tenant);
            if (String.IsNullOrWhiteSpace(tenant.Id)) throw new ArgumentException("Tenant ID is required.", nameof(tenant));
            token.ThrowIfCancellationRequested();
            tenant.LastUpdateUtc = DateTime.UtcNow;

            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    await UpsertTenantRowAsync(connection, transaction, tenant, token).ConfigureAwait(false);
                    await ReplaceSubordinateServicesAsync(connection, transaction, tenant, token).ConfigureAwait(false);
                    await transaction.CommitAsync(token).ConfigureAwait(false);
                }
            }

            return tenant;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) return false;
            token.ThrowIfCancellationRequested();

            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    using (SqlCommand childCommand = connection.CreateCommand())
                    {
                        childCommand.Transaction = transaction;
                        childCommand.CommandText = "DELETE FROM dbo.tenant_subordinate_services WHERE tenant_id = @id;";
                        childCommand.Parameters.AddWithValue("@id", id);
                        await childCommand.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }

                    int rows;
                    using (SqlCommand command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = "DELETE FROM dbo.tenants WHERE id = @id;";
                        command.Parameters.AddWithValue("@id", id);
                        rows = await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }

                    await transaction.CommitAsync(token).ConfigureAwait(false);
                    return rows > 0;
                }
            }
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) return false;
            token.ThrowIfCancellationRequested();
            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT TOP (1) 1 FROM dbo.tenants WHERE id = @id;";
                    command.Parameters.AddWithValue("@id", id);
                    object? value = await command.ExecuteScalarAsync(token).ConfigureAwait(false);
                    return value != null;
                }
            }
        }

        #endregion

        #region Private-Methods

        private const string TenantColumns =
            "id, name, active, default_client_shared, consume_shared, creation_ip_address, created_utc, last_update_utc";

        private const string SubordinateColumns =
            "tenant_id, service_name, operation, xeno_tenant_id, external_tenant_id, status, error_message, updated_utc";

        private static async Task UpsertTenantRowAsync(SqlConnection connection, SqlTransaction transaction, Tenant tenant, CancellationToken token)
        {
            using (SqlCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
IF EXISTS (SELECT 1 FROM dbo.tenants WHERE id = @id)
    UPDATE dbo.tenants SET
        name = @name,
        active = @active,
        default_client_shared = @default_client_shared,
        consume_shared = @consume_shared,
        creation_ip_address = @creation_ip_address,
        last_update_utc = @last_update_utc
    WHERE id = @id;
ELSE
    INSERT INTO dbo.tenants (id, name, active, default_client_shared, consume_shared, creation_ip_address, created_utc, last_update_utc)
    VALUES (@id, @name, @active, @default_client_shared, @consume_shared, @creation_ip_address, @created_utc, @last_update_utc);
""";
                command.Parameters.AddWithValue("@id", tenant.Id);
                command.Parameters.AddWithValue("@name", tenant.Name ?? String.Empty);
                command.Parameters.AddWithValue("@active", tenant.Active);
                command.Parameters.AddWithValue("@default_client_shared", tenant.DefaultClientShared);
                command.Parameters.AddWithValue("@consume_shared", tenant.ConsumeShared);
                command.Parameters.AddWithValue("@creation_ip_address", (object?)tenant.CreationIpAddress ?? DBNull.Value);
                command.Parameters.AddWithValue("@created_utc", FormatUtc(tenant.CreatedUtc));
                command.Parameters.AddWithValue("@last_update_utc", FormatUtc(tenant.LastUpdateUtc));
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }

        private static async Task ReplaceSubordinateServicesAsync(SqlConnection connection, SqlTransaction transaction, Tenant tenant, CancellationToken token)
        {
            using (SqlCommand delete = connection.CreateCommand())
            {
                delete.Transaction = transaction;
                delete.CommandText = "DELETE FROM dbo.tenant_subordinate_services WHERE tenant_id = @id;";
                delete.Parameters.AddWithValue("@id", tenant.Id);
                await delete.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }

            if (tenant.SubordinateServices == null || tenant.SubordinateServices.Count == 0) return;

            int ordinal = 0;
            foreach (TenantSubordinateServiceStatus service in tenant.SubordinateServices)
            {
                using (SqlCommand insert = connection.CreateCommand())
                {
                    insert.Transaction = transaction;
                    insert.CommandText = """
INSERT INTO dbo.tenant_subordinate_services (tenant_id, ordinal, service_name, operation, xeno_tenant_id, external_tenant_id, status, error_message, updated_utc)
VALUES (@tenant_id, @ordinal, @service_name, @operation, @xeno_tenant_id, @external_tenant_id, @status, @error_message, @updated_utc);
""";
                    insert.Parameters.AddWithValue("@tenant_id", tenant.Id);
                    insert.Parameters.AddWithValue("@ordinal", ordinal);
                    insert.Parameters.AddWithValue("@service_name", service.ServiceName ?? String.Empty);
                    insert.Parameters.AddWithValue("@operation", service.Operation ?? String.Empty);
                    insert.Parameters.AddWithValue("@xeno_tenant_id", service.XenoTenantId ?? String.Empty);
                    insert.Parameters.AddWithValue("@external_tenant_id", (object?)service.ExternalTenantId ?? DBNull.Value);
                    insert.Parameters.AddWithValue("@status", service.Status ?? String.Empty);
                    insert.Parameters.AddWithValue("@error_message", (object?)service.ErrorMessage ?? DBNull.Value);
                    insert.Parameters.AddWithValue("@updated_utc", FormatUtc(service.UpdatedUtc));
                    await insert.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    ordinal++;
                }
            }
        }

        private static async Task<List<TenantSubordinateServiceStatus>> ReadSubordinateServicesAsync(SqlConnection connection, string tenantId, CancellationToken token)
        {
            List<TenantSubordinateServiceStatus> services = new List<TenantSubordinateServiceStatus>();
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT " + SubordinateColumns + " FROM dbo.tenant_subordinate_services WHERE tenant_id = @id ORDER BY ordinal ASC;";
                command.Parameters.AddWithValue("@id", tenantId);
                using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false)) services.Add(HydrateSubordinate(reader));
                }
            }

            return services;
        }

        private static Tenant HydrateTenant(DbDataReader reader)
        {
            return new Tenant
            {
                Id = reader.GetString(0),
                Name = reader.IsDBNull(1) ? String.Empty : reader.GetString(1),
                Active = reader.GetBoolean(2),
                DefaultClientShared = reader.GetBoolean(3),
                ConsumeShared = reader.GetBoolean(4),
                CreationIpAddress = reader.IsDBNull(5) ? null : reader.GetString(5),
                CreatedUtc = ParseUtc(reader.GetString(6)),
                LastUpdateUtc = ParseUtc(reader.GetString(7)),
                SubordinateServices = new List<TenantSubordinateServiceStatus>()
            };
        }

        private static TenantSubordinateServiceStatus HydrateSubordinate(DbDataReader reader)
        {
            return new TenantSubordinateServiceStatus
            {
                ServiceName = reader.IsDBNull(1) ? String.Empty : reader.GetString(1),
                Operation = reader.IsDBNull(2) ? String.Empty : reader.GetString(2),
                XenoTenantId = reader.IsDBNull(3) ? String.Empty : reader.GetString(3),
                ExternalTenantId = reader.IsDBNull(4) ? null : reader.GetString(4),
                Status = reader.IsDBNull(5) ? String.Empty : reader.GetString(5),
                ErrorMessage = reader.IsDBNull(6) ? null : reader.GetString(6),
                UpdatedUtc = ParseUtc(reader.GetString(7))
            };
        }

        private static string FormatUtc(DateTime value)
        {
            return value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture);
        }

        private static DateTime ParseUtc(string value)
        {
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime parsed))
            {
                return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            }

            return DateTime.UtcNow;
        }

        private static string SchemaSql()
        {
            return """
IF OBJECT_ID(N'dbo.tenants', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tenants
    (
        id nvarchar(64) NOT NULL PRIMARY KEY,
        name nvarchar(256) NOT NULL,
        active bit NOT NULL DEFAULT 1,
        default_client_shared bit NOT NULL DEFAULT 0,
        consume_shared bit NOT NULL DEFAULT 1,
        creation_ip_address nvarchar(128) NULL,
        created_utc nvarchar(64) NOT NULL,
        last_update_utc nvarchar(64) NOT NULL
    );
END;

IF OBJECT_ID(N'dbo.tenant_subordinate_services', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tenant_subordinate_services
    (
        tenant_id nvarchar(64) NOT NULL,
        ordinal int NOT NULL,
        service_name nvarchar(256) NOT NULL,
        operation nvarchar(64) NOT NULL,
        xeno_tenant_id nvarchar(64) NOT NULL,
        external_tenant_id nvarchar(128) NULL,
        status nvarchar(64) NOT NULL,
        error_message nvarchar(max) NULL,
        updated_utc nvarchar(64) NOT NULL,
        CONSTRAINT pk_tenant_subordinate_services PRIMARY KEY (tenant_id, ordinal),
        CONSTRAINT fk_tenant_subordinate_services_tenant FOREIGN KEY (tenant_id) REFERENCES dbo.tenants(id) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_tenants_name' AND object_id = OBJECT_ID(N'dbo.tenants'))
    CREATE INDEX idx_tenants_name ON dbo.tenants(name);
""";
        }

        #endregion
    }
}

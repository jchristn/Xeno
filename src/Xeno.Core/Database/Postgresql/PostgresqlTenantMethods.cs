namespace Xeno.Core.Database.Postgresql
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Npgsql;
    using Xeno.Core.Database.Interfaces;
    using Xeno.Core.Helpers;
    using Xeno.Core.Models;

    /// <summary>
    /// PostgreSQL tenant data access methods. Tenant scalar fields map to columns on <c>tenants</c>
    /// and the <see cref="Tenant.SubordinateServices"/> collection maps to the ordered child table
    /// <c>tenant_subordinate_services</c>.
    /// </summary>
    public class PostgresqlTenantMethods : ITenantMethods
    {
        #region Private-Members

        private readonly string _ConnectionString;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="connectionString">Connection string.</param>
        public PostgresqlTenantMethods(string connectionString)
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
        public static async Task InitializeAsync(NpgsqlConnection connection, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(connection);
            token.ThrowIfCancellationRequested();
            using (NpgsqlCommand command = connection.CreateCommand())
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

            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (NpgsqlTransaction transaction = (NpgsqlTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false))
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
            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                Tenant? tenant = null;
                using (NpgsqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + TenantColumns + " FROM tenants WHERE id = @id LIMIT 1;";
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

            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                using (NpgsqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + TenantColumns + " FROM tenants ORDER BY created_utc ASC, id ASC;";
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
                    using (NpgsqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT " + SubordinateColumns + " FROM tenant_subordinate_services ORDER BY tenant_id ASC, ordinal ASC;";
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

            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (NpgsqlTransaction transaction = (NpgsqlTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false))
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

            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (NpgsqlTransaction transaction = (NpgsqlTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    using (NpgsqlCommand childCommand = connection.CreateCommand())
                    {
                        childCommand.Transaction = transaction;
                        childCommand.CommandText = "DELETE FROM tenant_subordinate_services WHERE tenant_id = @id;";
                        childCommand.Parameters.AddWithValue("@id", id);
                        await childCommand.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }

                    int rows;
                    using (NpgsqlCommand command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = "DELETE FROM tenants WHERE id = @id;";
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
            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (NpgsqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT 1 FROM tenants WHERE id = @id LIMIT 1;";
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

        private static async Task UpsertTenantRowAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Tenant tenant, CancellationToken token)
        {
            using (NpgsqlCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
INSERT INTO tenants (id, name, active, default_client_shared, consume_shared, creation_ip_address, created_utc, last_update_utc)
VALUES (@id, @name, @active, @default_client_shared, @consume_shared, @creation_ip_address, @created_utc, @last_update_utc)
ON CONFLICT (id) DO UPDATE SET
    name = EXCLUDED.name,
    active = EXCLUDED.active,
    default_client_shared = EXCLUDED.default_client_shared,
    consume_shared = EXCLUDED.consume_shared,
    creation_ip_address = EXCLUDED.creation_ip_address,
    last_update_utc = EXCLUDED.last_update_utc;
""";
                command.Parameters.AddWithValue("@id", tenant.Id);
                command.Parameters.AddWithValue("@name", tenant.Name ?? String.Empty);
                command.Parameters.AddWithValue("@active", tenant.Active ? 1 : 0);
                command.Parameters.AddWithValue("@default_client_shared", tenant.DefaultClientShared ? 1 : 0);
                command.Parameters.AddWithValue("@consume_shared", tenant.ConsumeShared ? 1 : 0);
                command.Parameters.AddWithValue("@creation_ip_address", (object?)tenant.CreationIpAddress ?? DBNull.Value);
                command.Parameters.AddWithValue("@created_utc", FormatUtc(tenant.CreatedUtc));
                command.Parameters.AddWithValue("@last_update_utc", FormatUtc(tenant.LastUpdateUtc));
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }

        private static async Task ReplaceSubordinateServicesAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Tenant tenant, CancellationToken token)
        {
            using (NpgsqlCommand delete = connection.CreateCommand())
            {
                delete.Transaction = transaction;
                delete.CommandText = "DELETE FROM tenant_subordinate_services WHERE tenant_id = @id;";
                delete.Parameters.AddWithValue("@id", tenant.Id);
                await delete.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }

            if (tenant.SubordinateServices == null || tenant.SubordinateServices.Count == 0) return;

            int ordinal = 0;
            foreach (TenantSubordinateServiceStatus service in tenant.SubordinateServices)
            {
                using (NpgsqlCommand insert = connection.CreateCommand())
                {
                    insert.Transaction = transaction;
                    insert.CommandText = """
INSERT INTO tenant_subordinate_services (tenant_id, ordinal, service_name, operation, xeno_tenant_id, external_tenant_id, status, error_message, updated_utc)
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

        private static async Task<List<TenantSubordinateServiceStatus>> ReadSubordinateServicesAsync(NpgsqlConnection connection, string tenantId, CancellationToken token)
        {
            List<TenantSubordinateServiceStatus> services = new List<TenantSubordinateServiceStatus>();
            using (NpgsqlCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT " + SubordinateColumns + " FROM tenant_subordinate_services WHERE tenant_id = @id ORDER BY ordinal ASC;";
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
                Active = reader.GetInt32(2) != 0,
                DefaultClientShared = reader.GetInt32(3) != 0,
                ConsumeShared = reader.GetInt32(4) != 0,
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
CREATE TABLE IF NOT EXISTS tenants
(
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    active INTEGER NOT NULL DEFAULT 1,
    default_client_shared INTEGER NOT NULL DEFAULT 0,
    consume_shared INTEGER NOT NULL DEFAULT 1,
    creation_ip_address TEXT NULL,
    created_utc TEXT NOT NULL,
    last_update_utc TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_tenants_name ON tenants(name);

CREATE TABLE IF NOT EXISTS tenant_subordinate_services
(
    tenant_id TEXT NOT NULL,
    ordinal INTEGER NOT NULL,
    service_name TEXT NOT NULL,
    operation TEXT NOT NULL,
    xeno_tenant_id TEXT NOT NULL,
    external_tenant_id TEXT NULL,
    status TEXT NOT NULL,
    error_message TEXT NULL,
    updated_utc TEXT NOT NULL,
    PRIMARY KEY (tenant_id, ordinal),
    FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE
);
""";
        }

        #endregion
    }
}

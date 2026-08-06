namespace Xeno.Core.Database.Mysql
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using MySql.Data.MySqlClient;
    using Xeno.Core.Database.Interfaces;
    using Xeno.Core.Enums;
    using Xeno.Core.Helpers;
    using Xeno.Core.Models;

    /// <summary>
    /// MySQL permission data access methods. Permission scalar fields map to columns on
    /// <c>permissions</c>. Permissions are tenant-owned, so every query filters by <c>tenant_id</c>.
    /// </summary>
    public class MysqlPermissionMethods : IPermissionMethods
    {
        #region Private-Members

        private readonly string _ConnectionString;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="connectionString">Connection string.</param>
        public MysqlPermissionMethods(string connectionString)
        {
            if (String.IsNullOrEmpty(connectionString)) throw new ArgumentNullException(nameof(connectionString));
            _ConnectionString = connectionString;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Create the permission tables and indexes if they do not exist.
        /// </summary>
        /// <param name="connection">Open connection.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public static async Task InitializeAsync(MySqlConnection connection, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(connection);
            token.ThrowIfCancellationRequested();
            using (MySqlCommand command = connection.CreateCommand())
            {
                command.CommandText = SchemaSql();
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public async Task<Permission> CreateAsync(Permission permission, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(permission);
            token.ThrowIfCancellationRequested();
            if (String.IsNullOrWhiteSpace(permission.Id)) permission.Id = IdGenerator.NewId("perm_");

            using (MySqlConnection connection = new MySqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (MySqlTransaction transaction = (MySqlTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    await UpsertPermissionRowAsync(connection, transaction, permission, token).ConfigureAwait(false);
                    await transaction.CommitAsync(token).ConfigureAwait(false);
                }
            }

            return permission;
        }

        /// <inheritdoc />
        public async Task<Permission?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) return null;
            if (String.IsNullOrWhiteSpace(id)) return null;
            token.ThrowIfCancellationRequested();
            using (MySqlConnection connection = new MySqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                using (MySqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + PermissionColumns + " FROM permissions WHERE tenant_id = @tenant_id AND id = @id LIMIT 1;";
                    command.Parameters.AddWithValue("@tenant_id", tenantId);
                    command.Parameters.AddWithValue("@id", id);
                    using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        if (await reader.ReadAsync(token).ConfigureAwait(false)) return HydratePermission(reader);
                    }
                }

                return null;
            }
        }

        /// <inheritdoc />
        public async Task<List<Permission>> EnumerateAsync(string tenantId, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            List<Permission> permissions = new List<Permission>();
            if (String.IsNullOrWhiteSpace(tenantId)) return permissions;

            using (MySqlConnection connection = new MySqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                using (MySqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + PermissionColumns + " FROM permissions WHERE tenant_id = @tenant_id ORDER BY created_utc ASC, id ASC;";
                    command.Parameters.AddWithValue("@tenant_id", tenantId);
                    using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(token).ConfigureAwait(false)) permissions.Add(HydratePermission(reader));
                    }
                }
            }

            return permissions;
        }

        /// <inheritdoc />
        public async Task<Permission> UpdateAsync(Permission permission, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(permission);
            if (String.IsNullOrWhiteSpace(permission.Id)) throw new ArgumentException("Permission ID is required.", nameof(permission));
            token.ThrowIfCancellationRequested();
            permission.LastUpdateUtc = DateTime.UtcNow;

            using (MySqlConnection connection = new MySqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (MySqlTransaction transaction = (MySqlTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    await UpsertPermissionRowAsync(connection, transaction, permission, token).ConfigureAwait(false);
                    await transaction.CommitAsync(token).ConfigureAwait(false);
                }
            }

            return permission;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) return false;
            if (String.IsNullOrWhiteSpace(id)) return false;
            token.ThrowIfCancellationRequested();

            using (MySqlConnection connection = new MySqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (MySqlTransaction transaction = (MySqlTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    int rows;
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = "DELETE FROM permissions WHERE tenant_id = @tenant_id AND id = @id;";
                        command.Parameters.AddWithValue("@tenant_id", tenantId);
                        command.Parameters.AddWithValue("@id", id);
                        rows = await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }

                    await transaction.CommitAsync(token).ConfigureAwait(false);
                    return rows > 0;
                }
            }
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) return false;
            if (String.IsNullOrWhiteSpace(id)) return false;
            token.ThrowIfCancellationRequested();
            using (MySqlConnection connection = new MySqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (MySqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT 1 FROM permissions WHERE tenant_id = @tenant_id AND id = @id LIMIT 1;";
                    command.Parameters.AddWithValue("@tenant_id", tenantId);
                    command.Parameters.AddWithValue("@id", id);
                    object? value = await command.ExecuteScalarAsync(token).ConfigureAwait(false);
                    return value != null;
                }
            }
        }

        #endregion

        #region Private-Methods

        private const string PermissionColumns =
            "id, tenant_id, resource_type, operation_type, permit, built_in, created_utc, last_update_utc, active";

        private static async Task UpsertPermissionRowAsync(MySqlConnection connection, MySqlTransaction transaction, Permission permission, CancellationToken token)
        {
            using (MySqlCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
INSERT INTO permissions (id, tenant_id, resource_type, operation_type, permit, built_in, created_utc, last_update_utc, active)
VALUES (@id, @tenant_id, @resource_type, @operation_type, @permit, @built_in, @created_utc, @last_update_utc, @active)
ON DUPLICATE KEY UPDATE
    tenant_id = VALUES(tenant_id),
    resource_type = VALUES(resource_type),
    operation_type = VALUES(operation_type),
    permit = VALUES(permit),
    built_in = VALUES(built_in),
    last_update_utc = VALUES(last_update_utc),
    active = VALUES(active);
""";
                command.Parameters.AddWithValue("@id", permission.Id);
                command.Parameters.AddWithValue("@tenant_id", permission.TenantId ?? String.Empty);
                command.Parameters.AddWithValue("@resource_type", permission.ResourceType ?? String.Empty);
                command.Parameters.AddWithValue("@operation_type", permission.OperationType.ToString());
                command.Parameters.AddWithValue("@permit", permission.Permit ? 1 : 0);
                command.Parameters.AddWithValue("@built_in", permission.BuiltIn ? 1 : 0);
                command.Parameters.AddWithValue("@created_utc", FormatUtc(permission.CreatedUtc));
                command.Parameters.AddWithValue("@last_update_utc", FormatUtc(permission.LastUpdateUtc));
                command.Parameters.AddWithValue("@active", permission.Active ? 1 : 0);
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }

        private static Permission HydratePermission(DbDataReader reader)
        {
            return new Permission
            {
                Id = reader.GetString(0),
                TenantId = reader.IsDBNull(1) ? String.Empty : reader.GetString(1),
                ResourceType = reader.IsDBNull(2) ? String.Empty : reader.GetString(2),
                OperationType = ParseEnum(reader, 3, OperationTypeEnum.Read),
                Permit = reader.GetInt32(4) != 0,
                BuiltIn = reader.GetInt32(5) != 0,
                CreatedUtc = ParseUtc(reader.GetString(6)),
                LastUpdateUtc = ParseUtc(reader.GetString(7)),
                Active = reader.GetInt32(8) != 0
            };
        }

        private static OperationTypeEnum ParseEnum(DbDataReader reader, int ordinal, OperationTypeEnum fallback)
        {
            if (reader.IsDBNull(ordinal)) return fallback;
            return Enum.TryParse(reader.GetString(ordinal), out OperationTypeEnum parsed) ? parsed : fallback;
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
CREATE TABLE IF NOT EXISTS permissions
(
    id varchar(64) NOT NULL PRIMARY KEY,
    tenant_id varchar(64) NOT NULL,
    resource_type varchar(256) NOT NULL,
    operation_type varchar(64) NOT NULL,
    permit INT NOT NULL DEFAULT 1,
    built_in INT NOT NULL DEFAULT 0,
    created_utc varchar(64) NOT NULL,
    last_update_utc varchar(64) NOT NULL,
    active INT NOT NULL DEFAULT 1,
    INDEX idx_permissions_tenant_id (tenant_id),
    INDEX idx_permissions_tenant_resource_operation (tenant_id, resource_type, operation_type)
);
""";
        }

        #endregion
    }
}

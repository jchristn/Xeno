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
    using Xeno.Core.Enums;
    using Xeno.Core.Helpers;
    using Xeno.Core.Models;

    /// <summary>
    /// SQL Server permission data access methods. Permission scalar fields map to columns on
    /// <c>dbo.permissions</c>. Permissions are tenant-owned, so every query filters by <c>tenant_id</c>.
    /// </summary>
    public class SqlServerPermissionMethods : IPermissionMethods
    {
        #region Private-Members

        private readonly string _ConnectionString;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="connectionString">Connection string.</param>
        public SqlServerPermissionMethods(string connectionString)
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
        public async Task<Permission> CreateAsync(Permission permission, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(permission);
            token.ThrowIfCancellationRequested();
            if (String.IsNullOrWhiteSpace(permission.Id)) permission.Id = IdGenerator.NewId("perm_");

            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false))
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
            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT TOP (1) " + PermissionColumns + " FROM dbo.permissions WHERE tenant_id = @tenant_id AND id = @id;";
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

            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + PermissionColumns + " FROM dbo.permissions WHERE tenant_id = @tenant_id ORDER BY created_utc ASC, id ASC;";
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

            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false))
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

            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    int rows;
                    using (SqlCommand command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = "DELETE FROM dbo.permissions WHERE tenant_id = @tenant_id AND id = @id;";
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
        public async Task<int> DeleteByTenantAsync(string tenantId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) return 0;
            token.ThrowIfCancellationRequested();

            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM dbo.permissions WHERE tenant_id = @tenant_id;";
                    command.Parameters.AddWithValue("@tenant_id", tenantId);
                    return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) return false;
            if (String.IsNullOrWhiteSpace(id)) return false;
            token.ThrowIfCancellationRequested();
            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT TOP (1) 1 FROM dbo.permissions WHERE tenant_id = @tenant_id AND id = @id;";
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

        private static async Task UpsertPermissionRowAsync(SqlConnection connection, SqlTransaction transaction, Permission permission, CancellationToken token)
        {
            using (SqlCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
IF EXISTS (SELECT 1 FROM dbo.permissions WHERE id = @id)
    UPDATE dbo.permissions SET
        tenant_id = @tenant_id,
        resource_type = @resource_type,
        operation_type = @operation_type,
        permit = @permit,
        built_in = @built_in,
        last_update_utc = @last_update_utc,
        active = @active
    WHERE id = @id;
ELSE
    INSERT INTO dbo.permissions (id, tenant_id, resource_type, operation_type, permit, built_in, created_utc, last_update_utc, active)
    VALUES (@id, @tenant_id, @resource_type, @operation_type, @permit, @built_in, @created_utc, @last_update_utc, @active);
""";
                command.Parameters.AddWithValue("@id", permission.Id);
                command.Parameters.AddWithValue("@tenant_id", permission.TenantId ?? String.Empty);
                command.Parameters.AddWithValue("@resource_type", permission.ResourceType ?? String.Empty);
                command.Parameters.AddWithValue("@operation_type", permission.OperationType.ToString());
                command.Parameters.AddWithValue("@permit", permission.Permit);
                command.Parameters.AddWithValue("@built_in", permission.BuiltIn);
                command.Parameters.AddWithValue("@created_utc", FormatUtc(permission.CreatedUtc));
                command.Parameters.AddWithValue("@last_update_utc", FormatUtc(permission.LastUpdateUtc));
                command.Parameters.AddWithValue("@active", permission.Active);
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
                Permit = reader.GetBoolean(4),
                BuiltIn = reader.GetBoolean(5),
                CreatedUtc = ParseUtc(reader.GetString(6)),
                LastUpdateUtc = ParseUtc(reader.GetString(7)),
                Active = reader.GetBoolean(8)
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
IF OBJECT_ID(N'dbo.permissions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.permissions
    (
        id nvarchar(64) NOT NULL PRIMARY KEY,
        tenant_id nvarchar(64) NOT NULL,
        resource_type nvarchar(256) NOT NULL,
        operation_type nvarchar(64) NOT NULL,
        permit bit NOT NULL DEFAULT 1,
        built_in bit NOT NULL DEFAULT 0,
        created_utc nvarchar(64) NOT NULL,
        last_update_utc nvarchar(64) NOT NULL,
        active bit NOT NULL DEFAULT 1
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_permissions_tenant_id' AND object_id = OBJECT_ID(N'dbo.permissions'))
    CREATE INDEX idx_permissions_tenant_id ON dbo.permissions(tenant_id);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_permissions_tenant_resource_operation' AND object_id = OBJECT_ID(N'dbo.permissions'))
    CREATE INDEX idx_permissions_tenant_resource_operation ON dbo.permissions(tenant_id, resource_type, operation_type);
""";
        }

        #endregion
    }
}

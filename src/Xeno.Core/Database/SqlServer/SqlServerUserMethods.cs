namespace Xeno.Core.Database.SqlServer
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.SqlClient;
    using Xeno.Core.Database.Interfaces;
    using Xeno.Core.Helpers;
    using Xeno.Core.Models;

    /// <summary>
    /// SQL Server user data access methods. User scalar fields map to columns on <c>dbo.users</c>. Users
    /// are tenant-owned; every read, update, and delete is scoped by tenant identifier.
    /// </summary>
    public class SqlServerUserMethods : IUserMethods
    {
        #region Private-Members

        private readonly string _ConnectionString;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="connectionString">Connection string.</param>
        public SqlServerUserMethods(string connectionString)
        {
            if (String.IsNullOrEmpty(connectionString)) throw new ArgumentNullException(nameof(connectionString));
            _ConnectionString = connectionString;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Create the user tables and indexes if they do not exist.
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
        public async Task<User> CreateAsync(User user, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(user);
            token.ThrowIfCancellationRequested();
            if (String.IsNullOrWhiteSpace(user.Id)) user.Id = IdGenerator.NewId("usr_");

            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    await UpsertUserRowAsync(connection, transaction, user, token).ConfigureAwait(false);
                    await transaction.CommitAsync(token).ConfigureAwait(false);
                }
            }

            return user;
        }

        /// <inheritdoc />
        public async Task<User?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) return null;
            if (String.IsNullOrWhiteSpace(id)) return null;
            token.ThrowIfCancellationRequested();
            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT TOP (1) " + UserColumns + " FROM dbo.users WHERE tenant_id = @tenant_id AND id = @id;";
                    command.Parameters.AddWithValue("@tenant_id", tenantId);
                    command.Parameters.AddWithValue("@id", id);
                    using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        if (await reader.ReadAsync(token).ConfigureAwait(false)) return HydrateUser(reader);
                    }
                }
            }

            return null;
        }

        /// <inheritdoc />
        public async Task<User?> ReadByUsernameAsync(string tenantId, string username, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) return null;
            if (String.IsNullOrWhiteSpace(username)) return null;
            token.ThrowIfCancellationRequested();
            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT TOP (1) " + UserColumns + " FROM dbo.users WHERE tenant_id = @tenant_id AND LOWER(username) = LOWER(@username);";
                    command.Parameters.AddWithValue("@tenant_id", tenantId);
                    command.Parameters.AddWithValue("@username", username);
                    using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        if (await reader.ReadAsync(token).ConfigureAwait(false)) return HydrateUser(reader);
                    }
                }
            }

            return null;
        }

        /// <inheritdoc />
        public async Task<User?> ReadByEmailAsync(string tenantId, string email, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) return null;
            if (String.IsNullOrWhiteSpace(email)) return null;
            token.ThrowIfCancellationRequested();
            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT TOP (1) " + UserColumns + " FROM dbo.users WHERE tenant_id = @tenant_id AND LOWER(email) = LOWER(@email);";
                    command.Parameters.AddWithValue("@tenant_id", tenantId);
                    command.Parameters.AddWithValue("@email", email);
                    using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        if (await reader.ReadAsync(token).ConfigureAwait(false)) return HydrateUser(reader);
                    }
                }
            }

            return null;
        }

        /// <inheritdoc />
        public async Task<List<User>> EnumerateAsync(string tenantId, CancellationToken token = default)
        {
            List<User> users = new List<User>();
            if (String.IsNullOrWhiteSpace(tenantId)) return users;
            token.ThrowIfCancellationRequested();

            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + UserColumns + " FROM dbo.users WHERE tenant_id = @tenant_id ORDER BY username ASC, id ASC;";
                    command.Parameters.AddWithValue("@tenant_id", tenantId);
                    using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(token).ConfigureAwait(false)) users.Add(HydrateUser(reader));
                    }
                }
            }

            return users;
        }

        /// <inheritdoc />
        public async Task<User> UpdateAsync(User user, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(user);
            if (String.IsNullOrWhiteSpace(user.Id)) throw new ArgumentException("User ID is required.", nameof(user));
            token.ThrowIfCancellationRequested();

            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    await UpsertUserRowAsync(connection, transaction, user, token).ConfigureAwait(false);
                    await transaction.CommitAsync(token).ConfigureAwait(false);
                }
            }

            return user;
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
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM dbo.users WHERE tenant_id = @tenant_id AND id = @id;";
                    command.Parameters.AddWithValue("@tenant_id", tenantId);
                    command.Parameters.AddWithValue("@id", id);
                    int rows = await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
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
            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT TOP (1) 1 FROM dbo.users WHERE tenant_id = @tenant_id AND id = @id;";
                    command.Parameters.AddWithValue("@tenant_id", tenantId);
                    command.Parameters.AddWithValue("@id", id);
                    object? value = await command.ExecuteScalarAsync(token).ConfigureAwait(false);
                    return value != null;
                }
            }
        }

        #endregion

        #region Private-Methods

        private const string UserColumns =
            "id, tenant_id, username, email, password_salt, password_hash, is_admin, is_tenant_admin, active";

        private static async Task UpsertUserRowAsync(SqlConnection connection, SqlTransaction transaction, User user, CancellationToken token)
        {
            using (SqlCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
IF EXISTS (SELECT 1 FROM dbo.users WHERE id = @id)
    UPDATE dbo.users SET
        tenant_id = @tenant_id,
        username = @username,
        email = @email,
        password_salt = @password_salt,
        password_hash = @password_hash,
        is_admin = @is_admin,
        is_tenant_admin = @is_tenant_admin,
        active = @active
    WHERE id = @id;
ELSE
    INSERT INTO dbo.users (id, tenant_id, username, email, password_salt, password_hash, is_admin, is_tenant_admin, active)
    VALUES (@id, @tenant_id, @username, @email, @password_salt, @password_hash, @is_admin, @is_tenant_admin, @active);
""";
                command.Parameters.AddWithValue("@id", user.Id);
                command.Parameters.AddWithValue("@tenant_id", user.TenantId ?? String.Empty);
                command.Parameters.AddWithValue("@username", user.Username ?? String.Empty);
                command.Parameters.AddWithValue("@email", user.Email ?? String.Empty);
                command.Parameters.AddWithValue("@password_salt", user.PasswordSalt ?? String.Empty);
                command.Parameters.AddWithValue("@password_hash", user.PasswordHash ?? String.Empty);
                command.Parameters.AddWithValue("@is_admin", user.IsAdmin);
                command.Parameters.AddWithValue("@is_tenant_admin", user.IsTenantAdmin);
                command.Parameters.AddWithValue("@active", user.Active);
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }

        private static User HydrateUser(DbDataReader reader)
        {
            return new User
            {
                Id = reader.GetString(0),
                TenantId = reader.IsDBNull(1) ? String.Empty : reader.GetString(1),
                Username = reader.IsDBNull(2) ? String.Empty : reader.GetString(2),
                Email = reader.IsDBNull(3) ? String.Empty : reader.GetString(3),
                PasswordSalt = reader.IsDBNull(4) ? String.Empty : reader.GetString(4),
                PasswordHash = reader.IsDBNull(5) ? String.Empty : reader.GetString(5),
                IsAdmin = reader.GetBoolean(6),
                IsTenantAdmin = reader.GetBoolean(7),
                Active = reader.GetBoolean(8)
            };
        }

        private static string SchemaSql()
        {
            return """
IF OBJECT_ID(N'dbo.users', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.users
    (
        id nvarchar(64) NOT NULL PRIMARY KEY,
        tenant_id nvarchar(64) NOT NULL,
        username nvarchar(256) NOT NULL,
        email nvarchar(256) NOT NULL,
        password_salt nvarchar(256) NOT NULL,
        password_hash nvarchar(256) NOT NULL,
        is_admin bit NOT NULL DEFAULT 0,
        is_tenant_admin bit NOT NULL DEFAULT 0,
        active bit NOT NULL DEFAULT 1
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_users_tenant_id' AND object_id = OBJECT_ID(N'dbo.users'))
    CREATE INDEX idx_users_tenant_id ON dbo.users(tenant_id);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_users_tenant_username' AND object_id = OBJECT_ID(N'dbo.users'))
    CREATE UNIQUE INDEX idx_users_tenant_username ON dbo.users(tenant_id, username);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_users_tenant_email' AND object_id = OBJECT_ID(N'dbo.users'))
    CREATE UNIQUE INDEX idx_users_tenant_email ON dbo.users(tenant_id, email);
""";
        }

        #endregion
    }
}

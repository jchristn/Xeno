namespace Xeno.Core.Database.Postgresql
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Threading;
    using System.Threading.Tasks;
    using Npgsql;
    using Xeno.Core.Database.Interfaces;
    using Xeno.Core.Helpers;
    using Xeno.Core.Models;

    /// <summary>
    /// PostgreSQL user data access methods. User scalar fields map to columns on <c>users</c>. Users are
    /// tenant-owned; every read, update, and delete is scoped by tenant identifier.
    /// </summary>
    public class PostgresqlUserMethods : IUserMethods
    {
        #region Private-Members

        private readonly string _ConnectionString;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="connectionString">Connection string.</param>
        public PostgresqlUserMethods(string connectionString)
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
        public async Task<User> CreateAsync(User user, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(user);
            token.ThrowIfCancellationRequested();
            if (String.IsNullOrWhiteSpace(user.Id)) user.Id = IdGenerator.NewId("usr_");

            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (NpgsqlTransaction transaction = (NpgsqlTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false))
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
            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (NpgsqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + UserColumns + " FROM users WHERE tenant_id = @tenant_id AND id = @id LIMIT 1;";
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
            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (NpgsqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + UserColumns + " FROM users WHERE tenant_id = @tenant_id AND LOWER(username) = LOWER(@username) LIMIT 1;";
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
            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (NpgsqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + UserColumns + " FROM users WHERE tenant_id = @tenant_id AND LOWER(email) = LOWER(@email) LIMIT 1;";
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

            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (NpgsqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + UserColumns + " FROM users WHERE tenant_id = @tenant_id ORDER BY username ASC, id ASC;";
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

            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (NpgsqlTransaction transaction = (NpgsqlTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false))
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

            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (NpgsqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM users WHERE tenant_id = @tenant_id AND id = @id;";
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
            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (NpgsqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT 1 FROM users WHERE tenant_id = @tenant_id AND id = @id LIMIT 1;";
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

        private static async Task UpsertUserRowAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, User user, CancellationToken token)
        {
            using (NpgsqlCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
INSERT INTO users (id, tenant_id, username, email, password_salt, password_hash, is_admin, is_tenant_admin, active)
VALUES (@id, @tenant_id, @username, @email, @password_salt, @password_hash, @is_admin, @is_tenant_admin, @active)
ON CONFLICT (id) DO UPDATE SET
    tenant_id = EXCLUDED.tenant_id,
    username = EXCLUDED.username,
    email = EXCLUDED.email,
    password_salt = EXCLUDED.password_salt,
    password_hash = EXCLUDED.password_hash,
    is_admin = EXCLUDED.is_admin,
    is_tenant_admin = EXCLUDED.is_tenant_admin,
    active = EXCLUDED.active;
""";
                command.Parameters.AddWithValue("@id", user.Id);
                command.Parameters.AddWithValue("@tenant_id", user.TenantId ?? String.Empty);
                command.Parameters.AddWithValue("@username", user.Username ?? String.Empty);
                command.Parameters.AddWithValue("@email", user.Email ?? String.Empty);
                command.Parameters.AddWithValue("@password_salt", user.PasswordSalt ?? String.Empty);
                command.Parameters.AddWithValue("@password_hash", user.PasswordHash ?? String.Empty);
                command.Parameters.AddWithValue("@is_admin", user.IsAdmin ? 1 : 0);
                command.Parameters.AddWithValue("@is_tenant_admin", user.IsTenantAdmin ? 1 : 0);
                command.Parameters.AddWithValue("@active", user.Active ? 1 : 0);
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
                IsAdmin = reader.GetInt32(6) != 0,
                IsTenantAdmin = reader.GetInt32(7) != 0,
                Active = reader.GetInt32(8) != 0
            };
        }

        private static string SchemaSql()
        {
            return """
CREATE TABLE IF NOT EXISTS users
(
    id TEXT PRIMARY KEY,
    tenant_id TEXT NOT NULL,
    username TEXT NOT NULL,
    email TEXT NOT NULL,
    password_salt TEXT NOT NULL,
    password_hash TEXT NOT NULL,
    is_admin INTEGER NOT NULL DEFAULT 0,
    is_tenant_admin INTEGER NOT NULL DEFAULT 0,
    active INTEGER NOT NULL DEFAULT 1
);

CREATE INDEX IF NOT EXISTS idx_users_tenant_id ON users(tenant_id);
CREATE UNIQUE INDEX IF NOT EXISTS idx_users_tenant_username ON users(tenant_id, username);
CREATE UNIQUE INDEX IF NOT EXISTS idx_users_tenant_email ON users(tenant_id, email);
""";
        }

        #endregion
    }
}

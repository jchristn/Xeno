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
    /// SQL Server authenticated-session data access methods. Session scalar fields map to columns on
    /// <c>dbo.auth_sessions</c>. The <c>token_hash</c> column is globally unique, so lookups by token
    /// hash and expiry-based pruning are intentionally not tenant-scoped.
    /// </summary>
    public class SqlServerAuthSessionMethods : IAuthSessionMethods
    {
        #region Private-Members

        private readonly string _ConnectionString;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="connectionString">Connection string.</param>
        public SqlServerAuthSessionMethods(string connectionString)
        {
            if (String.IsNullOrEmpty(connectionString)) throw new ArgumentNullException(nameof(connectionString));
            _ConnectionString = connectionString;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Create the authenticated-session table and indexes if they do not exist.
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
        public async Task<AuthSession> CreateAsync(AuthSession session, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(session);
            token.ThrowIfCancellationRequested();
            if (String.IsNullOrWhiteSpace(session.Id)) session.Id = IdGenerator.NewId("sess_");

            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = UpsertSql();
                    AddSessionParameters(command, session);
                    await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }

            return session;
        }

        /// <inheritdoc />
        public async Task<AuthSession?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) return null;
            token.ThrowIfCancellationRequested();
            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT TOP (1) " + SessionColumns + " FROM dbo.auth_sessions WHERE tenant_id = @tenant_id AND id = @id;";
                    command.Parameters.AddWithValue("@tenant_id", tenantId ?? String.Empty);
                    command.Parameters.AddWithValue("@id", id);
                    using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        if (await reader.ReadAsync(token).ConfigureAwait(false)) return HydrateSession(reader);
                    }
                }
            }

            return null;
        }

        /// <inheritdoc />
        public async Task<AuthSession?> ReadByTokenHashAsync(string tokenHash, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tokenHash)) return null;
            token.ThrowIfCancellationRequested();
            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT TOP (1) " + SessionColumns + " FROM dbo.auth_sessions WHERE token_hash = @token_hash;";
                    command.Parameters.AddWithValue("@token_hash", tokenHash);
                    using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        if (await reader.ReadAsync(token).ConfigureAwait(false)) return HydrateSession(reader);
                    }
                }
            }

            return null;
        }

        /// <inheritdoc />
        public async Task<AuthSession?> ReadByIdAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) return null;
            token.ThrowIfCancellationRequested();
            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT TOP (1) " + SessionColumns + " FROM dbo.auth_sessions WHERE id = @id;";
                    command.Parameters.AddWithValue("@id", id);
                    using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        if (await reader.ReadAsync(token).ConfigureAwait(false)) return HydrateSession(reader);
                    }
                }
            }

            return null;
        }

        /// <inheritdoc />
        public async Task<List<AuthSession>> EnumerateByTenantAsync(string tenantId, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            List<AuthSession> sessions = new List<AuthSession>();

            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + SessionColumns + " FROM dbo.auth_sessions WHERE tenant_id = @tenant_id ORDER BY created_utc ASC, id ASC;";
                    command.Parameters.AddWithValue("@tenant_id", tenantId ?? String.Empty);
                    using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(token).ConfigureAwait(false)) sessions.Add(HydrateSession(reader));
                    }
                }
            }

            return sessions;
        }

        /// <inheritdoc />
        public async Task<AuthSession> UpdateAsync(AuthSession session, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(session);
            if (String.IsNullOrWhiteSpace(session.Id)) throw new ArgumentException("Session ID is required.", nameof(session));
            token.ThrowIfCancellationRequested();
            session.LastUpdateUtc = DateTime.UtcNow;

            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = UpsertSql();
                    AddSessionParameters(command, session);
                    await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }

            return session;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) return false;
            token.ThrowIfCancellationRequested();
            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM dbo.auth_sessions WHERE tenant_id = @tenant_id AND id = @id;";
                    command.Parameters.AddWithValue("@tenant_id", tenantId ?? String.Empty);
                    command.Parameters.AddWithValue("@id", id);
                    int rows = await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    return rows > 0;
                }
            }
        }

        /// <inheritdoc />
        public async Task<int> DeleteByUserAsync(string tenantId, string userId, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM dbo.auth_sessions WHERE tenant_id = @tenant_id AND user_id = @user_id;";
                    command.Parameters.AddWithValue("@tenant_id", tenantId ?? String.Empty);
                    command.Parameters.AddWithValue("@user_id", userId ?? String.Empty);
                    return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }
        }

        /// <inheritdoc />
        public async Task<int> DeleteByCredentialAsync(string tenantId, string credentialId, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM dbo.auth_sessions WHERE tenant_id = @tenant_id AND credential_id = @credential_id;";
                    command.Parameters.AddWithValue("@tenant_id", tenantId ?? String.Empty);
                    command.Parameters.AddWithValue("@credential_id", credentialId ?? String.Empty);
                    return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }
        }

        /// <inheritdoc />
        public async Task<int> PruneExpiredAsync(DateTime cutoffUtc, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM dbo.auth_sessions WHERE expires_utc < @cutoff;";
                    command.Parameters.AddWithValue("@cutoff", FormatUtc(cutoffUtc));
                    return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }
        }

        #endregion

        #region Private-Methods

        private const string SessionColumns =
            "id, tenant_id, principal_type, user_id, credential_id, token_hash, auth_scheme, created_utc, last_update_utc, expires_utc, active";

        private static string UpsertSql()
        {
            return """
IF EXISTS (SELECT 1 FROM dbo.auth_sessions WHERE id = @id)
    UPDATE dbo.auth_sessions SET
        tenant_id = @tenant_id,
        principal_type = @principal_type,
        user_id = @user_id,
        credential_id = @credential_id,
        token_hash = @token_hash,
        auth_scheme = @auth_scheme,
        last_update_utc = @last_update_utc,
        expires_utc = @expires_utc,
        active = @active
    WHERE id = @id;
ELSE
    INSERT INTO dbo.auth_sessions (id, tenant_id, principal_type, user_id, credential_id, token_hash, auth_scheme, created_utc, last_update_utc, expires_utc, active)
    VALUES (@id, @tenant_id, @principal_type, @user_id, @credential_id, @token_hash, @auth_scheme, @created_utc, @last_update_utc, @expires_utc, @active);
""";
        }

        private static void AddSessionParameters(SqlCommand command, AuthSession session)
        {
            command.Parameters.AddWithValue("@id", session.Id);
            command.Parameters.AddWithValue("@tenant_id", session.TenantId ?? String.Empty);
            command.Parameters.AddWithValue("@principal_type", session.PrincipalType.ToString());
            command.Parameters.AddWithValue("@user_id", (object?)session.UserId ?? DBNull.Value);
            command.Parameters.AddWithValue("@credential_id", (object?)session.CredentialId ?? DBNull.Value);
            command.Parameters.AddWithValue("@token_hash", session.TokenHash ?? String.Empty);
            command.Parameters.AddWithValue("@auth_scheme", session.AuthScheme.ToString());
            command.Parameters.AddWithValue("@created_utc", FormatUtc(session.CreatedUtc));
            command.Parameters.AddWithValue("@last_update_utc", FormatUtc(session.LastUpdateUtc));
            command.Parameters.AddWithValue("@expires_utc", FormatUtc(session.ExpiresUtc));
            command.Parameters.AddWithValue("@active", session.Active);
        }

        private static AuthSession HydrateSession(DbDataReader reader)
        {
            return new AuthSession
            {
                Id = reader.GetString(0),
                TenantId = reader.IsDBNull(1) ? String.Empty : reader.GetString(1),
                PrincipalType = ParsePrincipalType(reader, 2, PrincipalTypeEnum.User),
                UserId = reader.IsDBNull(3) ? null : reader.GetString(3),
                CredentialId = reader.IsDBNull(4) ? null : reader.GetString(4),
                TokenHash = reader.IsDBNull(5) ? String.Empty : reader.GetString(5),
                AuthScheme = ParseAuthScheme(reader, 6, AuthSchemeEnum.Credential),
                CreatedUtc = ParseUtc(reader.GetString(7)),
                LastUpdateUtc = ParseUtc(reader.GetString(8)),
                ExpiresUtc = ParseUtc(reader.GetString(9)),
                Active = reader.GetBoolean(10)
            };
        }

        private static PrincipalTypeEnum ParsePrincipalType(DbDataReader reader, int ordinal, PrincipalTypeEnum fallback)
        {
            if (reader.IsDBNull(ordinal)) return fallback;
            return Enum.TryParse(reader.GetString(ordinal), out PrincipalTypeEnum parsed) ? parsed : fallback;
        }

        private static AuthSchemeEnum ParseAuthScheme(DbDataReader reader, int ordinal, AuthSchemeEnum fallback)
        {
            if (reader.IsDBNull(ordinal)) return fallback;
            return Enum.TryParse(reader.GetString(ordinal), out AuthSchemeEnum parsed) ? parsed : fallback;
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
IF OBJECT_ID(N'dbo.auth_sessions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.auth_sessions
    (
        id nvarchar(64) NOT NULL PRIMARY KEY,
        tenant_id nvarchar(64) NOT NULL,
        principal_type nvarchar(64) NOT NULL,
        user_id nvarchar(64) NULL,
        credential_id nvarchar(64) NULL,
        token_hash nvarchar(512) NOT NULL,
        auth_scheme nvarchar(64) NOT NULL,
        created_utc nvarchar(64) NOT NULL,
        last_update_utc nvarchar(64) NOT NULL,
        expires_utc nvarchar(64) NOT NULL,
        active bit NOT NULL DEFAULT 1
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_auth_sessions_token_hash' AND object_id = OBJECT_ID(N'dbo.auth_sessions'))
    CREATE UNIQUE INDEX idx_auth_sessions_token_hash ON dbo.auth_sessions(token_hash);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_auth_sessions_tenant' AND object_id = OBJECT_ID(N'dbo.auth_sessions'))
    CREATE INDEX idx_auth_sessions_tenant ON dbo.auth_sessions(tenant_id);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_auth_sessions_tenant_user' AND object_id = OBJECT_ID(N'dbo.auth_sessions'))
    CREATE INDEX idx_auth_sessions_tenant_user ON dbo.auth_sessions(tenant_id, user_id);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_auth_sessions_tenant_credential' AND object_id = OBJECT_ID(N'dbo.auth_sessions'))
    CREATE INDEX idx_auth_sessions_tenant_credential ON dbo.auth_sessions(tenant_id, credential_id);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_auth_sessions_expires' AND object_id = OBJECT_ID(N'dbo.auth_sessions'))
    CREATE INDEX idx_auth_sessions_expires ON dbo.auth_sessions(expires_utc);
""";
        }

        #endregion
    }
}

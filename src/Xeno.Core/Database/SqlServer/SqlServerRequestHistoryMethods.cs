namespace Xeno.Core.Database.SqlServer
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Globalization;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.SqlClient;
    using Xeno.Core.Database.Interfaces;
    using Xeno.Core.Enums;
    using Xeno.Core.Helpers;
    using Xeno.Core.Models;
    using Xeno.Core.Serialization;

    /// <summary>
    /// SQL Server request history database methods.
    /// </summary>
    public class SqlServerRequestHistoryMethods : IRequestHistoryMethods
    {
        private readonly string _ConnectionString;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="connectionString">Connection string.</param>
        public SqlServerRequestHistoryMethods(string connectionString)
        {
            if (String.IsNullOrEmpty(connectionString)) throw new ArgumentNullException(nameof(connectionString));
            _ConnectionString = connectionString;
        }

        /// <summary>
        /// Create the request history table and indexes if they do not exist.
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
        public async Task InsertAsync(RequestHistoryEntry entry, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(entry);
            token.ThrowIfCancellationRequested();
            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = InsertSql();
                    AddInsertParameters(command, entry);
                    await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }
        }

        /// <inheritdoc />
        public async Task<RequestHistoryEntry?> GetAsync(string tenantId, string id, CancellationToken token)
        {
            if (String.IsNullOrEmpty(id)) return null;
            token.ThrowIfCancellationRequested();
            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT TOP (1) entry_json FROM request_history WHERE id = @id AND tenant_id = @tenant;";
                    command.Parameters.AddWithValue("@id", id);
                    command.Parameters.AddWithValue("@tenant", tenantId ?? String.Empty);
                    object? value = await command.ExecuteScalarAsync(token).ConfigureAwait(false);
                    string? json = value as string;
                    if (String.IsNullOrEmpty(json)) return null;
                    return JsonUtil.Deserialize<RequestHistoryEntry>(json);
                }
            }
        }

        /// <inheritdoc />
        public async Task<RequestHistoryPage> QueryAsync(RequestHistoryQuery query, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(query);
            token.ThrowIfCancellationRequested();
            RequestHistoryPage page = new RequestHistoryPage();
            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                using (SqlCommand countCommand = connection.CreateCommand())
                {
                    StringBuilder where = new StringBuilder();
                    AppendFilter(countCommand, query, where);
                    countCommand.CommandText = "SELECT COUNT(*) FROM request_history" + where.ToString() + ";";
                    object? countValue = await countCommand.ExecuteScalarAsync(token).ConfigureAwait(false);
                    page.Total = Convert.ToInt32(countValue ?? 0, CultureInfo.InvariantCulture);
                }

                using (SqlCommand command = connection.CreateCommand())
                {
                    StringBuilder where = new StringBuilder();
                    AppendFilter(command, query, where);
                    string columns = query.IncludeBody ? "entry_json" : ProjectionColumns;
                    string order = query.OrderDescending ? "DESC" : "ASC";
                    string sql = "SELECT " + columns + " FROM request_history" + where.ToString() + " ORDER BY created_utc " + order + ", id " + order;
                    if (query.Limit > 0)
                    {
                        sql += " OFFSET @offset ROWS FETCH NEXT @limit ROWS ONLY";
                        command.Parameters.AddWithValue("@offset", Math.Max(0, query.Offset));
                        command.Parameters.AddWithValue("@limit", query.Limit);
                    }

                    command.CommandText = sql + ";";
                    using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(token).ConfigureAwait(false))
                        {
                            RequestHistoryEntry? entry = query.IncludeBody ? HydrateFromJson(reader) : HydrateFromColumns(reader);
                            if (entry != null) page.Items.Add(entry);
                        }
                    }
                }
            }

            return page;
        }

        /// <inheritdoc />
        public async Task<int> CountAsync(RequestHistoryQuery query, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(query);
            token.ThrowIfCancellationRequested();
            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlCommand command = connection.CreateCommand())
                {
                    StringBuilder where = new StringBuilder();
                    AppendFilter(command, query, where);
                    command.CommandText = "SELECT COUNT(*) FROM request_history" + where.ToString() + ";";
                    object? value = await command.ExecuteScalarAsync(token).ConfigureAwait(false);
                    return Convert.ToInt32(value ?? 0, CultureInfo.InvariantCulture);
                }
            }
        }

        /// <inheritdoc />
        public async Task<int> DeleteAsync(RequestHistoryQuery query, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(query);
            token.ThrowIfCancellationRequested();
            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlCommand command = connection.CreateCommand())
                {
                    StringBuilder where = new StringBuilder();
                    AppendFilter(command, query, where);
                    command.CommandText = "DELETE FROM request_history" + where.ToString() + ";";
                    return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }
        }

        /// <inheritdoc />
        public async Task<int> AnonymizeUserAsync(string tenantId, string userId, IReadOnlyCollection<string> credentialIds, IReadOnlyCollection<string> sessionIds, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            RequestHistoryQuery query = new RequestHistoryQuery
            {
                TenantId = tenantId,
                MatchRequestorTenant = true,
                UserId = userId,
                CredentialIds = credentialIds,
                SessionIds = sessionIds,
                IncludeBody = true
            };

            int updated = 0;
            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                List<RequestHistoryEntry> entries = new List<RequestHistoryEntry>();
                using (SqlCommand selectCommand = connection.CreateCommand())
                {
                    StringBuilder where = new StringBuilder();
                    AppendFilter(selectCommand, query, where);
                    selectCommand.CommandText = "SELECT entry_json FROM request_history" + where.ToString() + ";";
                    using (DbDataReader reader = await selectCommand.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(token).ConfigureAwait(false))
                        {
                            RequestHistoryEntry? entry = HydrateFromJson(reader);
                            if (entry != null) entries.Add(entry);
                        }
                    }
                }

                foreach (RequestHistoryEntry entry in entries)
                {
                    RequestHistoryAnonymizer.Anonymize(entry, userId, credentialIds, sessionIds);
                    using (SqlCommand update = connection.CreateCommand())
                    {
                        update.CommandText = "UPDATE request_history SET user_id = @user_id, credential_id = @credential_id, session_id = @session_id, requestor_principal_id = @requestor_principal_id, entry_json = @entry_json WHERE id = @id;";
                        update.Parameters.AddWithValue("@user_id", (object?)entry.UserId ?? DBNull.Value);
                        update.Parameters.AddWithValue("@credential_id", (object?)entry.CredentialId ?? DBNull.Value);
                        update.Parameters.AddWithValue("@session_id", (object?)entry.SessionId ?? DBNull.Value);
                        update.Parameters.AddWithValue("@requestor_principal_id", (object?)entry.RequestorPrincipalId ?? DBNull.Value);
                        update.Parameters.AddWithValue("@entry_json", JsonUtil.Serialize(entry));
                        update.Parameters.AddWithValue("@id", entry.Id);
                        updated += await update.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }
                }
            }

            return updated;
        }

        /// <inheritdoc />
        public async Task<int> PruneOlderThanAsync(DateTime cutoffUtc, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM request_history WHERE created_utc < @cutoff;";
                    command.Parameters.AddWithValue("@cutoff", FormatUtc(cutoffUtc));
                    return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }
        }

        private const string ProjectionColumns =
            "id, tenant_id, requestor_tenant_id, created_utc, completed_utc, path, method, model, selected_client_id, status_code, duration_ms, source_ip, user_id, credential_id, session_id, principal_type, requestor_principal_type, requestor_principal_id, auth_scheme, total_tokens";

        private static string InsertSql()
        {
            return """
INSERT INTO request_history
    (id, tenant_id, requestor_tenant_id, created_utc, completed_utc, path, method, model, selected_client_id, status_code, duration_ms, source_ip, user_id, credential_id, session_id, principal_type, requestor_principal_type, requestor_principal_id, auth_scheme, total_tokens, entry_json)
VALUES
    (@id, @tenant_id, @requestor_tenant_id, @created_utc, @completed_utc, @path, @method, @model, @selected_client_id, @status_code, @duration_ms, @source_ip, @user_id, @credential_id, @session_id, @principal_type, @requestor_principal_type, @requestor_principal_id, @auth_scheme, @total_tokens, @entry_json);
""";
        }

        private static void AddInsertParameters(SqlCommand command, RequestHistoryEntry entry)
        {
            command.Parameters.AddWithValue("@id", entry.Id);
            command.Parameters.AddWithValue("@tenant_id", entry.TenantId ?? String.Empty);
            command.Parameters.AddWithValue("@requestor_tenant_id", entry.RequestorTenantId ?? String.Empty);
            command.Parameters.AddWithValue("@created_utc", FormatUtc(entry.CreatedUtc));
            command.Parameters.AddWithValue("@completed_utc", entry.CompletedUtc.HasValue ? FormatUtc(entry.CompletedUtc.Value) : (object)DBNull.Value);
            command.Parameters.AddWithValue("@path", entry.Path ?? String.Empty);
            command.Parameters.AddWithValue("@method", entry.Method ?? String.Empty);
            command.Parameters.AddWithValue("@model", (object?)entry.Model ?? DBNull.Value);
            command.Parameters.AddWithValue("@selected_client_id", (object?)entry.SelectedClientId ?? DBNull.Value);
            command.Parameters.AddWithValue("@status_code", entry.StatusCode);
            command.Parameters.AddWithValue("@duration_ms", entry.DurationMs);
            command.Parameters.AddWithValue("@source_ip", (object?)entry.SourceIp ?? DBNull.Value);
            command.Parameters.AddWithValue("@user_id", (object?)entry.UserId ?? DBNull.Value);
            command.Parameters.AddWithValue("@credential_id", (object?)entry.CredentialId ?? DBNull.Value);
            command.Parameters.AddWithValue("@session_id", (object?)entry.SessionId ?? DBNull.Value);
            command.Parameters.AddWithValue("@principal_type", entry.PrincipalType.ToString());
            command.Parameters.AddWithValue("@requestor_principal_type", entry.RequestorPrincipalType.ToString());
            command.Parameters.AddWithValue("@requestor_principal_id", (object?)entry.RequestorPrincipalId ?? DBNull.Value);
            command.Parameters.AddWithValue("@auth_scheme", entry.AuthScheme.ToString());
            command.Parameters.AddWithValue("@total_tokens", entry.Telemetry?.TotalTokens.HasValue == true ? (object)entry.Telemetry.TotalTokens!.Value : DBNull.Value);
            command.Parameters.AddWithValue("@entry_json", JsonUtil.Serialize(entry));
        }

        private static void AppendFilter(SqlCommand command, RequestHistoryQuery query, StringBuilder where)
        {
            List<string> clauses = new List<string>();

            if (!String.IsNullOrEmpty(query.TenantId))
            {
                clauses.Add(query.MatchRequestorTenant ? "(tenant_id = @tenant OR requestor_tenant_id = @tenant)" : "tenant_id = @tenant");
                command.Parameters.AddWithValue("@tenant", query.TenantId);
            }

            if (query.Ids != null && query.Ids.Count > 0)
            {
                clauses.Add("id IN (" + AddInList(command, "id", query.Ids) + ")");
            }

            if (!String.IsNullOrEmpty(query.Model))
            {
                clauses.Add("model = @model");
                command.Parameters.AddWithValue("@model", query.Model);
            }

            if (!String.IsNullOrEmpty(query.Path))
            {
                clauses.Add("path = @path");
                command.Parameters.AddWithValue("@path", query.Path);
            }

            if (!String.IsNullOrEmpty(query.SelectedClientId))
            {
                clauses.Add("selected_client_id = @selected_client_id");
                command.Parameters.AddWithValue("@selected_client_id", query.SelectedClientId);
            }

            List<string> userClauses = new List<string>();
            if (!String.IsNullOrEmpty(query.UserId))
            {
                userClauses.Add("user_id = @user_ref");
                userClauses.Add("requestor_principal_id = @user_ref");
                command.Parameters.AddWithValue("@user_ref", query.UserId);
            }

            if (query.CredentialIds != null && query.CredentialIds.Count > 0)
            {
                userClauses.Add("credential_id IN (" + AddInList(command, "cred", query.CredentialIds) + ")");
            }

            if (query.SessionIds != null && query.SessionIds.Count > 0)
            {
                userClauses.Add("session_id IN (" + AddInList(command, "sess", query.SessionIds) + ")");
            }

            if (userClauses.Count > 0) clauses.Add("(" + String.Join(" OR ", userClauses) + ")");

            if (query.FromUtc.HasValue)
            {
                clauses.Add("created_utc >= @from_utc");
                command.Parameters.AddWithValue("@from_utc", FormatUtc(query.FromUtc.Value));
            }

            if (query.ToUtc.HasValue)
            {
                clauses.Add("created_utc < @to_utc");
                command.Parameters.AddWithValue("@to_utc", FormatUtc(query.ToUtc.Value));
            }

            if (query.MinStatusCode.HasValue)
            {
                clauses.Add("status_code >= @min_status");
                command.Parameters.AddWithValue("@min_status", query.MinStatusCode.Value);
            }

            if (query.MaxStatusCode.HasValue)
            {
                clauses.Add("status_code <= @max_status");
                command.Parameters.AddWithValue("@max_status", query.MaxStatusCode.Value);
            }

            if (query.RequireTokens) clauses.Add("(total_tokens IS NOT NULL AND total_tokens > 0)");

            if (clauses.Count > 0) where.Append(" WHERE ").Append(String.Join(" AND ", clauses));
        }

        private static string AddInList(SqlCommand command, string prefix, IReadOnlyCollection<string> values)
        {
            List<string> names = new List<string>();
            int index = 0;
            foreach (string value in values)
            {
                string name = "@" + prefix + index.ToString(CultureInfo.InvariantCulture);
                command.Parameters.AddWithValue(name, value ?? String.Empty);
                names.Add(name);
                index++;
            }

            return String.Join(", ", names);
        }

        private static RequestHistoryEntry? HydrateFromJson(DbDataReader reader)
        {
            string? json = reader.IsDBNull(0) ? null : reader.GetString(0);
            if (String.IsNullOrEmpty(json)) return null;
            return JsonUtil.Deserialize<RequestHistoryEntry>(json);
        }

        private static RequestHistoryEntry HydrateFromColumns(DbDataReader reader)
        {
            RequestHistoryEntry entry = new RequestHistoryEntry
            {
                Id = reader.GetString(0),
                TenantId = reader.GetString(1),
                RequestorTenantId = reader.GetString(2),
                CreatedUtc = ParseUtc(reader.GetString(3)),
                CompletedUtc = reader.IsDBNull(4) ? null : ParseUtc(reader.GetString(4)),
                Path = reader.IsDBNull(5) ? String.Empty : reader.GetString(5),
                Method = reader.IsDBNull(6) ? String.Empty : reader.GetString(6),
                Model = reader.IsDBNull(7) ? null : reader.GetString(7),
                SelectedClientId = reader.IsDBNull(8) ? null : reader.GetString(8),
                StatusCode = reader.GetInt32(9),
                DurationMs = reader.GetInt64(10),
                SourceIp = reader.IsDBNull(11) ? null : reader.GetString(11),
                UserId = reader.IsDBNull(12) ? null : reader.GetString(12),
                CredentialId = reader.IsDBNull(13) ? null : reader.GetString(13),
                SessionId = reader.IsDBNull(14) ? null : reader.GetString(14),
                PrincipalType = ParseEnum(reader, 15, PrincipalTypeEnum.None),
                RequestorPrincipalType = ParseEnum(reader, 16, PrincipalTypeEnum.None),
                RequestorPrincipalId = reader.IsDBNull(17) ? null : reader.GetString(17),
                AuthScheme = ParseAuthScheme(reader, 18)
            };

            if (!reader.IsDBNull(19)) entry.Telemetry.TotalTokens = reader.GetInt32(19);
            return entry;
        }

        private static PrincipalTypeEnum ParseEnum(DbDataReader reader, int ordinal, PrincipalTypeEnum fallback)
        {
            if (reader.IsDBNull(ordinal)) return fallback;
            return Enum.TryParse(reader.GetString(ordinal), out PrincipalTypeEnum parsed) ? parsed : fallback;
        }

        private static AuthSchemeEnum ParseAuthScheme(DbDataReader reader, int ordinal)
        {
            if (reader.IsDBNull(ordinal)) return AuthSchemeEnum.None;
            return Enum.TryParse(reader.GetString(ordinal), out AuthSchemeEnum parsed) ? parsed : AuthSchemeEnum.None;
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
IF OBJECT_ID(N'dbo.request_history', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.request_history
    (
        id nvarchar(64) NOT NULL PRIMARY KEY,
        tenant_id nvarchar(128) NOT NULL,
        requestor_tenant_id nvarchar(128) NOT NULL,
        created_utc nvarchar(64) NOT NULL,
        completed_utc nvarchar(64) NULL,
        path nvarchar(max) NULL,
        method nvarchar(16) NULL,
        model nvarchar(512) NULL,
        selected_client_id nvarchar(128) NULL,
        status_code int NOT NULL DEFAULT 0,
        duration_ms bigint NOT NULL DEFAULT 0,
        source_ip nvarchar(128) NULL,
        user_id nvarchar(128) NULL,
        credential_id nvarchar(128) NULL,
        session_id nvarchar(128) NULL,
        principal_type nvarchar(32) NULL,
        requestor_principal_type nvarchar(32) NULL,
        requestor_principal_id nvarchar(128) NULL,
        auth_scheme nvarchar(32) NULL,
        total_tokens int NULL,
        entry_json nvarchar(max) NOT NULL
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_request_history_tenant_created' AND object_id = OBJECT_ID(N'dbo.request_history'))
    CREATE INDEX idx_request_history_tenant_created ON dbo.request_history(tenant_id, created_utc);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_request_history_requestor_tenant_created' AND object_id = OBJECT_ID(N'dbo.request_history'))
    CREATE INDEX idx_request_history_requestor_tenant_created ON dbo.request_history(requestor_tenant_id, created_utc);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_request_history_tenant_model' AND object_id = OBJECT_ID(N'dbo.request_history'))
    CREATE INDEX idx_request_history_tenant_model ON dbo.request_history(tenant_id, model);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_request_history_created' AND object_id = OBJECT_ID(N'dbo.request_history'))
    CREATE INDEX idx_request_history_created ON dbo.request_history(created_utc);
""";
        }
    }
}

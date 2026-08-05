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
    using Xeno.Core.Models;
    using Xeno.Core.Serialization;

    /// <summary>
    /// SqlServer onboarding request database methods.
    /// </summary>
    public class SqlServerOnboardingMethods : IOnboardingMethods
    {
        private readonly string _ConnectionString;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="connectionString">Connection string.</param>
        public SqlServerOnboardingMethods(string connectionString)
        {
            if (String.IsNullOrEmpty(connectionString)) throw new ArgumentNullException(nameof(connectionString));
            _ConnectionString = connectionString;
        }

        /// <summary>
        /// Create the onboarding table and indexes if they do not exist.
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
        public async Task InsertAsync(OnboardingRequest request, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(request);
            token.ThrowIfCancellationRequested();
            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = """
INSERT INTO onboarding_requests (id, tenant_id, email, token, status, created_utc, record_json)
VALUES (@id, @tenant_id, @email, @token, @status, @created_utc, @record_json);
""";
                    AddParameters(command, request);
                    await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }
        }

        /// <inheritdoc />
        public async Task<int> UpdateAsync(OnboardingRequest request, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(request);
            token.ThrowIfCancellationRequested();
            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "UPDATE onboarding_requests SET status = @status, record_json = @record_json WHERE id = @id;";
                    command.Parameters.AddWithValue("@status", request.Status.ToString());
                    command.Parameters.AddWithValue("@record_json", JsonUtil.Serialize(request));
                    command.Parameters.AddWithValue("@id", request.Id);
                    return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }
        }

        /// <inheritdoc />
        public async Task<OnboardingRequest?> GetByTokenAsync(string verificationToken, CancellationToken token)
        {
            if (String.IsNullOrEmpty(verificationToken)) return null;
            return await GetSingleAsync("token = @value", verificationToken, "created_utc DESC", token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<OnboardingRequest?> GetByTenantAsync(string tenantId, CancellationToken token)
        {
            if (String.IsNullOrEmpty(tenantId)) return null;
            return await GetSingleAsync("tenant_id = @value", tenantId, "created_utc DESC", token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<OnboardingRecordPage> QueryAsync(OnboardingQuery query, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(query);
            token.ThrowIfCancellationRequested();
            OnboardingRecordPage page = new OnboardingRecordPage();
            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                using (SqlCommand countCommand = connection.CreateCommand())
                {
                    StringBuilder where = new StringBuilder();
                    AppendFilter(countCommand, query, where);
                    countCommand.CommandText = "SELECT COUNT(*) FROM onboarding_requests" + where.ToString() + ";";
                    object? countValue = await countCommand.ExecuteScalarAsync(token).ConfigureAwait(false);
                    page.Total = Convert.ToInt32(countValue ?? 0, CultureInfo.InvariantCulture);
                }

                using (SqlCommand command = connection.CreateCommand())
                {
                    StringBuilder where = new StringBuilder();
                    AppendFilter(command, query, where);
                    string order = query.OrderDescending ? "DESC" : "ASC";
                    string sql = "SELECT record_json FROM onboarding_requests" + where.ToString() + " ORDER BY created_utc " + order + ", id " + order;
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
                            OnboardingRequest? record = Hydrate(reader);
                            if (record != null) page.Items.Add(record);
                        }
                    }
                }
            }

            return page;
        }

        /// <inheritdoc />
        public async Task<int> CountAsync(OnboardingQuery query, CancellationToken token)
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
                    command.CommandText = "SELECT COUNT(*) FROM onboarding_requests" + where.ToString() + ";";
                    object? value = await command.ExecuteScalarAsync(token).ConfigureAwait(false);
                    return Convert.ToInt32(value ?? 0, CultureInfo.InvariantCulture);
                }
            }
        }

        /// <inheritdoc />
        public async Task<int> DeleteByTenantAsync(string tenantId, CancellationToken token)
        {
            if (String.IsNullOrEmpty(tenantId)) return 0;
            token.ThrowIfCancellationRequested();
            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM onboarding_requests WHERE tenant_id = @tenant;";
                    command.Parameters.AddWithValue("@tenant", tenantId);
                    return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }
        }

        private async Task<OnboardingRequest?> GetSingleAsync(string clause, string value, string order, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT TOP 1 record_json FROM onboarding_requests WHERE " + clause + " ORDER BY " + order + ";";
                    command.Parameters.AddWithValue("@value", value);
                    object? scalar = await command.ExecuteScalarAsync(token).ConfigureAwait(false);
                    string? json = scalar as string;
                    if (String.IsNullOrEmpty(json)) return null;
                    return JsonUtil.Deserialize<OnboardingRequest>(json);
                }
            }
        }

        private static void AddParameters(SqlCommand command, OnboardingRequest request)
        {
            command.Parameters.AddWithValue("@id", request.Id);
            command.Parameters.AddWithValue("@tenant_id", request.TenantId ?? String.Empty);
            command.Parameters.AddWithValue("@email", request.Email ?? String.Empty);
            command.Parameters.AddWithValue("@token", request.Token ?? String.Empty);
            command.Parameters.AddWithValue("@status", request.Status.ToString());
            command.Parameters.AddWithValue("@created_utc", FormatUtc(request.CreatedUtc));
            command.Parameters.AddWithValue("@record_json", JsonUtil.Serialize(request));
        }

        private static void AppendFilter(SqlCommand command, OnboardingQuery query, StringBuilder where)
        {
            List<string> clauses = new List<string>();
            if (query.Status.HasValue)
            {
                clauses.Add("status = @status");
                command.Parameters.AddWithValue("@status", query.Status.Value.ToString());
            }

            if (!String.IsNullOrEmpty(query.TenantId))
            {
                clauses.Add("tenant_id = @tenant");
                command.Parameters.AddWithValue("@tenant", query.TenantId);
            }

            if (clauses.Count > 0) where.Append(" WHERE ").Append(String.Join(" AND ", clauses));
        }

        private static OnboardingRequest? Hydrate(DbDataReader reader)
        {
            string? json = reader.IsDBNull(0) ? null : reader.GetString(0);
            if (String.IsNullOrEmpty(json)) return null;
            return JsonUtil.Deserialize<OnboardingRequest>(json);
        }

        private static string FormatUtc(DateTime value)
        {
            return value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture);
        }

        private static string SchemaSql()
        {
            return """
IF OBJECT_ID(N'dbo.onboarding_requests', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.onboarding_requests
    (
        id nvarchar(64) NOT NULL PRIMARY KEY,
        tenant_id nvarchar(128) NOT NULL,
        email nvarchar(320) NULL,
        token nvarchar(128) NOT NULL,
        status nvarchar(32) NOT NULL,
        created_utc nvarchar(64) NOT NULL,
        record_json nvarchar(max) NOT NULL
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_onboarding_requests_token' AND object_id = OBJECT_ID(N'dbo.onboarding_requests'))
    CREATE UNIQUE INDEX idx_onboarding_requests_token ON dbo.onboarding_requests(token);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_onboarding_requests_tenant' AND object_id = OBJECT_ID(N'dbo.onboarding_requests'))
    CREATE INDEX idx_onboarding_requests_tenant ON dbo.onboarding_requests(tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_onboarding_requests_status' AND object_id = OBJECT_ID(N'dbo.onboarding_requests'))
    CREATE INDEX idx_onboarding_requests_status ON dbo.onboarding_requests(status, created_utc);
""";
        }
    }
}

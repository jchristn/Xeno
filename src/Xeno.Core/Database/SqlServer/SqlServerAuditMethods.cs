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
    /// SQL Server audit record database methods.
    /// </summary>
    public class SqlServerAuditMethods : IAuditMethods
    {
        private readonly string _ConnectionString;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="connectionString">Connection string.</param>
        public SqlServerAuditMethods(string connectionString)
        {
            if (String.IsNullOrEmpty(connectionString)) throw new ArgumentNullException(nameof(connectionString));
            _ConnectionString = connectionString;
        }

        /// <summary>
        /// Create the audit table and indexes if they do not exist.
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
        public async Task InsertAsync(AuditRecord record, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(record);
            token.ThrowIfCancellationRequested();
            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = InsertSql();
                    AddInsertParameters(command, record);
                    await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }
        }

        /// <inheritdoc />
        public async Task<AuditRecordPage> QueryAsync(AuditQuery query, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(query);
            token.ThrowIfCancellationRequested();
            AuditRecordPage page = new AuditRecordPage();
            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                using (SqlCommand countCommand = connection.CreateCommand())
                {
                    StringBuilder where = new StringBuilder();
                    AppendFilter(countCommand, query, where);
                    countCommand.CommandText = "SELECT COUNT(*) FROM audit_records" + where.ToString() + ";";
                    object? countValue = await countCommand.ExecuteScalarAsync(token).ConfigureAwait(false);
                    page.Total = Convert.ToInt32(countValue ?? 0, CultureInfo.InvariantCulture);
                }

                using (SqlCommand command = connection.CreateCommand())
                {
                    StringBuilder where = new StringBuilder();
                    AppendFilter(command, query, where);
                    string order = query.OrderDescending ? "DESC" : "ASC";
                    string sql = "SELECT record_json FROM audit_records" + where.ToString() + " ORDER BY created_utc " + order + ", id " + order;
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
                            AuditRecord? record = Hydrate(reader);
                            if (record != null) page.Items.Add(record);
                        }
                    }
                }
            }

            return page;
        }

        /// <inheritdoc />
        public async Task<int> CountAsync(AuditQuery query, CancellationToken token)
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
                    command.CommandText = "SELECT COUNT(*) FROM audit_records" + where.ToString() + ";";
                    object? value = await command.ExecuteScalarAsync(token).ConfigureAwait(false);
                    return Convert.ToInt32(value ?? 0, CultureInfo.InvariantCulture);
                }
            }
        }

        /// <inheritdoc />
        public async Task<int> DeleteAsync(AuditQuery query, CancellationToken token)
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
                    command.CommandText = "DELETE FROM audit_records" + where.ToString() + ";";
                    return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }
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
                    command.CommandText = "DELETE FROM audit_records WHERE created_utc < @cutoff;";
                    command.Parameters.AddWithValue("@cutoff", FormatUtc(cutoffUtc));
                    return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }
        }

        private static string InsertSql()
        {
            return """
INSERT INTO audit_records
    (id, tenant_id, target_tenant_id, principal_id, target_resource_id, created_utc, record_json)
VALUES
    (@id, @tenant_id, @target_tenant_id, @principal_id, @target_resource_id, @created_utc, @record_json);
""";
        }

        private static void AddInsertParameters(SqlCommand command, AuditRecord record)
        {
            command.Parameters.AddWithValue("@id", record.Id);
            command.Parameters.AddWithValue("@tenant_id", record.TenantId ?? String.Empty);
            command.Parameters.AddWithValue("@target_tenant_id", (object?)record.TargetTenantId ?? DBNull.Value);
            command.Parameters.AddWithValue("@principal_id", (object?)record.PrincipalId ?? DBNull.Value);
            command.Parameters.AddWithValue("@target_resource_id", (object?)record.TargetResourceId ?? DBNull.Value);
            command.Parameters.AddWithValue("@created_utc", FormatUtc(record.CreatedUtc));
            command.Parameters.AddWithValue("@record_json", JsonUtil.Serialize(record));
        }

        private static void AppendFilter(SqlCommand command, AuditQuery query, StringBuilder where)
        {
            List<string> clauses = new List<string>();

            if (!String.IsNullOrEmpty(query.TenantId))
            {
                clauses.Add(query.MatchTargetTenant ? "(tenant_id = @tenant OR target_tenant_id = @tenant)" : "tenant_id = @tenant");
                command.Parameters.AddWithValue("@tenant", query.TenantId);
            }

            if (!String.IsNullOrEmpty(query.UserId))
            {
                clauses.Add("(principal_id = @user_ref OR target_resource_id = @user_ref)");
                command.Parameters.AddWithValue("@user_ref", query.UserId);
            }

            if (query.Ids != null && query.Ids.Count > 0)
            {
                clauses.Add("id IN (" + AddInList(command, "id", query.Ids) + ")");
            }

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

        private static AuditRecord? Hydrate(DbDataReader reader)
        {
            string? json = reader.IsDBNull(0) ? null : reader.GetString(0);
            if (String.IsNullOrEmpty(json)) return null;
            return JsonUtil.Deserialize<AuditRecord>(json);
        }

        private static string FormatUtc(DateTime value)
        {
            return value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture);
        }

        private static string SchemaSql()
        {
            return """
IF OBJECT_ID(N'dbo.audit_records', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.audit_records
    (
        id nvarchar(64) NOT NULL PRIMARY KEY,
        tenant_id nvarchar(128) NOT NULL,
        target_tenant_id nvarchar(128) NULL,
        principal_id nvarchar(128) NULL,
        target_resource_id nvarchar(128) NULL,
        created_utc nvarchar(64) NOT NULL,
        record_json nvarchar(max) NOT NULL
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_audit_records_tenant_created' AND object_id = OBJECT_ID(N'dbo.audit_records'))
    CREATE INDEX idx_audit_records_tenant_created ON dbo.audit_records(tenant_id, created_utc);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_audit_records_target_tenant' AND object_id = OBJECT_ID(N'dbo.audit_records'))
    CREATE INDEX idx_audit_records_target_tenant ON dbo.audit_records(target_tenant_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_audit_records_created' AND object_id = OBJECT_ID(N'dbo.audit_records'))
    CREATE INDEX idx_audit_records_created ON dbo.audit_records(created_utc);
""";
        }
    }
}

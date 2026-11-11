namespace Xeno.Core.Database
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Globalization;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Analytics;
    using Xeno.Core.Database.Interfaces;
    using Xeno.Core.Models;
    using Xeno.Core.Serialization;

    /// <summary>
    /// Typed analytics columns on <c>request_history</c>, shared by every database provider: their names, insert
    /// parameters, the usage read, and the backfill from <c>entry_json</c>. The SQL is plain ANSI with <c>@name</c>
    /// parameters, which SQLite, PostgreSQL, MySQL, and SQL Server all accept; only the row limit differs.
    /// </summary>
    public static class RequestHistoryUsageSql
    {
        #region Public-Members

        /// <summary>
        /// Analytics columns, in insert order.
        /// </summary>
        public static readonly IReadOnlyList<string> Columns = new List<string>
        {
            "ttft_ms", "streaming_ms", "client_round_trip_ms", "upstream_ms", "request_to_headers_ms", "prompt_tokens", "completion_tokens", "selected_provider", "usage_version"
        };

        /// <summary>
        /// Current backfill version written with every row.
        /// </summary>
        public const int UsageVersion = 1;

        /// <summary>
        /// Comma-separated column list for an INSERT, starting with a comma.
        /// </summary>
        public static string InsertColumns
        {
            get { return ", " + String.Join(", ", Columns); }
        }

        /// <summary>
        /// Comma-separated parameter list for an INSERT, starting with a comma.
        /// </summary>
        public static string InsertValues
        {
            get { return ", @" + String.Join(", @", Columns); }
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Column type for a column on a provider.
        /// </summary>
        /// <param name="column">Column name.</param>
        /// <param name="textType">The provider's type for short text (for example TEXT, varchar(255), nvarchar(255)).</param>
        /// <param name="bigType">The provider's 64-bit integer type.</param>
        /// <param name="intType">The provider's 32-bit integer type.</param>
        /// <returns>Type with NULL.</returns>
        public static string ColumnType(string column, string textType, string bigType, string intType)
        {
            if (column == "selected_provider") return textType + " NULL";
            if (column == "prompt_tokens" || column == "completion_tokens" || column == "usage_version") return intType + " NULL";
            return bigType + " NULL";
        }

        /// <summary>
        /// Add the analytics column parameters for an entry.
        /// </summary>
        /// <param name="command">Command.</param>
        /// <param name="entry">Entry.</param>
        /// <exception cref="ArgumentNullException">command or entry is null.</exception>
        public static void AddParameters(DbCommand command, RequestHistoryEntry entry)
        {
            ArgumentNullException.ThrowIfNull(command);
            ArgumentNullException.ThrowIfNull(entry);
            RequestHistoryTelemetry telemetry = entry.Telemetry ?? new RequestHistoryTelemetry();
            Add(command, "@ttft_ms", telemetry.TimeToFirstTokenMs);
            Add(command, "@streaming_ms", telemetry.TotalStreamingTimeMs);
            Add(command, "@client_round_trip_ms", telemetry.ClientRoundTripMs ?? entry.RoutingDecision?.ClientRoundTripMs);
            Add(command, "@upstream_ms", telemetry.UpstreamTotalMs);
            Add(command, "@request_to_headers_ms", telemetry.RequestToHeadersMs);
            Add(command, "@prompt_tokens", telemetry.PromptTokens);
            Add(command, "@completion_tokens", telemetry.CompletionTokens);
            Add(command, "@selected_provider", String.IsNullOrWhiteSpace(entry.RoutingDecision?.SelectedProvider) ? null : entry.RoutingDecision!.SelectedProvider);
            Add(command, "@usage_version", UsageVersion);
        }

        /// <summary>
        /// Read usage records for a tenant and range from typed columns only.
        /// </summary>
        /// <param name="connection">Open connection.</param>
        /// <param name="filter">Filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Records.</returns>
        /// <exception cref="ArgumentNullException">connection or filter is null.</exception>
        public static async IAsyncEnumerable<UsageRecord> EnumerateAsync(DbConnection connection, RequestHistoryUsageFilter filter, [EnumeratorCancellation] CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(connection);
            ArgumentNullException.ThrowIfNull(filter);
            using (DbCommand command = connection.CreateCommand())
            {
                StringBuilder sql = new StringBuilder("SELECT created_utc, model, selected_client_id, selected_provider, tenant_id, user_id, credential_id, status_code, duration_ms, ttft_ms, streaming_ms, client_round_trip_ms, upstream_ms, request_to_headers_ms, prompt_tokens, completion_tokens, total_tokens FROM request_history WHERE created_utc >= @from_utc AND created_utc < @to_utc AND ((path LIKE '/v1/%' AND path NOT LIKE '/v1/models%') OR path IN ('/api/chat', '/api/generate', '/api/embed', '/api/embeddings'))");
                Add(command, "@from_utc", FormatUtc(filter.FromUtc));
                Add(command, "@to_utc", FormatUtc(filter.ToUtc));
                if (filter.TenantId != null)
                {
                    sql.Append(" AND tenant_id = @tenant_id");
                    Add(command, "@tenant_id", filter.TenantId);
                }

                Clause(command, sql, "model", "@model", filter.Model);
                Clause(command, sql, "selected_client_id", "@client_id", filter.ClientId);
                Clause(command, sql, "user_id", "@user_id", filter.UserId);
                Clause(command, sql, "credential_id", "@credential_id", filter.CredentialId);
                command.CommandText = sql.ToString();
                using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        RequestHistoryEntry entry = new RequestHistoryEntry
                        {
                            CreatedUtc = ParseUtc(reader.GetString(0)),
                            Model = Text(reader, 1),
                            SelectedClientId = Text(reader, 2),
                            TenantId = Text(reader, 4) ?? String.Empty,
                            UserId = Text(reader, 5),
                            CredentialId = Text(reader, 6),
                            StatusCode = (int)(Long(reader, 7) ?? 0),
                            DurationMs = Long(reader, 8) ?? 0
                        };
                        entry.Telemetry.TimeToFirstTokenMs = Long(reader, 9);
                        entry.Telemetry.TotalStreamingTimeMs = Long(reader, 10);
                        entry.Telemetry.ClientRoundTripMs = Long(reader, 11);
                        entry.Telemetry.UpstreamTotalMs = Long(reader, 12);
                        entry.Telemetry.RequestToHeadersMs = Long(reader, 13);
                        entry.Telemetry.PromptTokens = Int(reader, 14);
                        entry.Telemetry.CompletionTokens = Int(reader, 15);
                        entry.Telemetry.TotalTokens = Int(reader, 16);
                        if (!entry.Telemetry.PromptTokens.HasValue && !entry.Telemetry.CompletionTokens.HasValue && entry.Telemetry.TotalTokens.HasValue) entry.Telemetry.PromptTokens = entry.Telemetry.TotalTokens;
                        yield return UsageRecordMapper.FromNetworkRequest(entry, Text(reader, 3));
                    }
                }
            }
        }

        /// <summary>
        /// Fill the analytics columns of up to <paramref name="batchSize"/> rows written before they existed, from
        /// their <c>entry_json</c>. Safe to stop and resume: each row is marked when done.
        /// </summary>
        /// <param name="connection">Open connection.</param>
        /// <param name="batchSize">Rows per call.</param>
        /// <param name="sqlServer">True for SQL Server, which limits rows with TOP instead of LIMIT.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Rows filled; 0 when nothing is left.</returns>
        /// <exception cref="ArgumentNullException">connection is null.</exception>
        public static async Task<int> BackfillAsync(DbConnection connection, int batchSize, bool sqlServer, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(connection);
            int limit = Math.Clamp(batchSize, 1, 50000);
            List<KeyValuePair<string, string>> rows = new List<KeyValuePair<string, string>>();
            using (DbCommand select = connection.CreateCommand())
            {
                select.CommandText = sqlServer
                    ? "SELECT TOP (" + limit.ToString(CultureInfo.InvariantCulture) + ") id, entry_json FROM request_history WHERE usage_version IS NULL"
                    : "SELECT id, entry_json FROM request_history WHERE usage_version IS NULL LIMIT " + limit.ToString(CultureInfo.InvariantCulture);
                using (DbDataReader reader = await select.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        rows.Add(new KeyValuePair<string, string>(reader.GetString(0), reader.IsDBNull(1) ? String.Empty : reader.GetString(1)));
                    }
                }
            }

            if (rows.Count == 0) return 0;
            using (DbTransaction transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false))
            {
                foreach (KeyValuePair<string, string> row in rows)
                {
                    RequestHistoryEntry entry = JsonUtil.TryDeserialize<RequestHistoryEntry>(row.Value, out RequestHistoryEntry? parsed) && parsed != null ? parsed : new RequestHistoryEntry();
                    using (DbCommand update = connection.CreateCommand())
                    {
                        update.Transaction = transaction;
                        List<string> sets = new List<string>();
                        foreach (string column in Columns) sets.Add(column + " = @" + column);
                        update.CommandText = "UPDATE request_history SET " + String.Join(", ", sets) + " WHERE id = @id";
                        AddParameters(update, entry);
                        Add(update, "@id", row.Key);
                        await update.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }
                }

                await transaction.CommitAsync(token).ConfigureAwait(false);
            }

            return rows.Count;
        }

        #endregion

        #region Private-Methods

        private static void Clause(DbCommand command, StringBuilder sql, string column, string name, string? value)
        {
            if (value == null) return;
            sql.Append(" AND ").Append(column).Append(" = ").Append(name);
            Add(command, name, value);
        }

        private static void Add(DbCommand command, string name, object? value)
        {
            DbParameter parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        private static string? Text(DbDataReader reader, int ordinal)
        {
            if (reader.IsDBNull(ordinal)) return null;
            string value = Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture) ?? String.Empty;
            return value.Length == 0 ? null : value;
        }

        private static long? Long(DbDataReader reader, int ordinal)
        {
            if (reader.IsDBNull(ordinal)) return null;
            return Convert.ToInt64(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
        }

        private static int? Int(DbDataReader reader, int ordinal)
        {
            long? value = Long(reader, ordinal);
            return value.HasValue ? (int)Math.Clamp(value.Value, Int32.MinValue, Int32.MaxValue) : null;
        }

        private static string FormatUtc(DateTime value)
        {
            return value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture);
        }

        private static DateTime ParseUtc(string value)
        {
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime parsed)) return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            return DateTime.MinValue;
        }

        #endregion
    }
}

namespace Xeno.Client.LocalDashboard.Database
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Globalization;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Sqlite;
    using SQLitePCL;
    using Xeno.Client.LocalDashboard.Database.Sqlite;
    using Xeno.Client.LocalDashboard.Models;
    using Xeno.Client.Routing;
    using Xeno.Core.Analytics;
    using Xeno.Core.Models;

    /// <summary>
    /// SQLite local request history methods.
    /// </summary>
    public sealed class SqliteLocalRequestHistoryMethods : ILocalRequestHistoryMethods, ILocalRequestSummaryMethods, ILocalProviderSnapshotMethods, ILocalModelSnapshotMethods, ILocalDashboardMetadataMethods
    {
        private readonly ClientLocalDatabaseSettings _Settings;
        private string _ConnectionString = string.Empty;

        /// <summary>
        /// Instantiate the SQLite local request-history methods.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        public SqliteLocalRequestHistoryMethods(ClientLocalDatabaseSettings settings)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <inheritdoc />
        public async Task InitializeAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            string filename = _Settings.ResolveFilename();
            string? directory = Path.GetDirectoryName(filename);
            if (!String.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            Batteries_V2.Init();
            _ConnectionString = String.IsNullOrWhiteSpace(_Settings.ConnectionString)
                ? "Data Source=" + filename + ";Pooling=False;Foreign Keys=True;"
                : _Settings.ConnectionString;

            using (SqliteConnection connection = await LocalSqliteConnection.OpenAsync(_ConnectionString, token).ConfigureAwait(false))
            {
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = SchemaSql();
                    await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }

                await EnsureRequestHistoryColumnsAsync(connection, token).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public async Task UpsertAsync(LocalRequestHistoryEntry entry, ClientLocalHistorySettings settings, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(entry);
            ArgumentNullException.ThrowIfNull(settings);
            token.ThrowIfCancellationRequested();
            int maxBodyBytes = Math.Max(0, settings.MaxBodyBytes);
            using (SqliteConnection connection = await LocalSqliteConnection.OpenAsync(_ConnectionString, token).ConfigureAwait(false))
            {
                using (SqliteTransaction transaction = connection.BeginTransaction())
                {
                    await UpsertMainAsync(connection, transaction, entry, token).ConfigureAwait(false);
                    await DeleteChildrenAsync(connection, transaction, entry.Id, token).ConfigureAwait(false);
                    await InsertHeadersAsync(connection, transaction, "local_request_headers", entry.Id, entry.RequestHeaders, token).ConfigureAwait(false);
                    await InsertHeadersAsync(connection, transaction, "local_response_headers", entry.Id, entry.ResponseHeaders, token).ConfigureAwait(false);
                    if (settings.PersistBodies)
                    {
                        await InsertBodyAsync(connection, transaction, "local_request_bodies", entry.Id, TruncateUtf8(entry.RequestBody, maxBodyBytes), token).ConfigureAwait(false);
                        await InsertBodyAsync(connection, transaction, "local_response_bodies", entry.Id, TruncateUtf8(entry.ResponseBody, maxBodyBytes), token).ConfigureAwait(false);
                    }

                    await InsertTelemetryAsync(connection, transaction, entry.Id, entry.Telemetry, token).ConfigureAwait(false);
                    await InsertTimelineAsync(connection, transaction, entry.Id, entry.Timeline, token).ConfigureAwait(false);
                    await PruneAsync(connection, transaction, Math.Max(1, settings.MaxEntries), token).ConfigureAwait(false);
                    await transaction.CommitAsync(token).ConfigureAwait(false);
                }
            }
        }

        private static async Task EnsureRequestHistoryColumnsAsync(SqliteConnection connection, CancellationToken token)
        {
            await TryAlterAsync(connection, "ALTER TABLE local_request_history ADD COLUMN requestor_tenant_id TEXT NULL;", token).ConfigureAwait(false);
            await TryAlterAsync(connection, "ALTER TABLE local_request_history ADD COLUMN requestor_principal_type TEXT NULL;", token).ConfigureAwait(false);
            await TryAlterAsync(connection, "ALTER TABLE local_request_history ADD COLUMN requestor_principal_id TEXT NULL;", token).ConfigureAwait(false);
            await TryAlterAsync(connection, "ALTER TABLE local_request_history ADD COLUMN provider_telemetry_sample_id TEXT NULL;", token).ConfigureAwait(false);
            await TryAlterAsync(connection, "ALTER TABLE local_request_history ADD COLUMN provider_telemetry_health_status TEXT NULL;", token).ConfigureAwait(false);
            await TryAlterAsync(connection, "ALTER TABLE local_request_history ADD COLUMN provider_telemetry_queued_requests REAL NULL;", token).ConfigureAwait(false);
        }

        private static async Task TryAlterAsync(SqliteConnection connection, string sql, CancellationToken token)
        {
            try
            {
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 1 && ex.Message.Contains("duplicate column", StringComparison.OrdinalIgnoreCase))
            {
            }
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<LocalRequestHistoryEntry>> ListAsync(int limit, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            List<LocalRequestHistoryEntry> entries = new List<LocalRequestHistoryEntry>();
            using (SqliteConnection connection = await LocalSqliteConnection.OpenAsync(_ConnectionString, token).ConfigureAwait(false))
            {
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM local_request_history ORDER BY received_utc DESC LIMIT $limit;";
                    command.Parameters.AddWithValue("$limit", Math.Max(1, limit));
                    using (SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(token).ConfigureAwait(false))
                        {
                            LocalRequestHistoryEntry entry = ReadMain(reader);
                            entries.Add(entry);
                        }
                    }
                }

                foreach (LocalRequestHistoryEntry entry in entries)
                {
                    await LoadChildrenAsync(connection, entry, token).ConfigureAwait(false);
                }
            }

            return entries;
        }

        /// <inheritdoc />
        public async Task<LocalRequestHistoryEntry?> GetAsync(string id, CancellationToken token)
        {
            if (String.IsNullOrWhiteSpace(id)) return null;
            token.ThrowIfCancellationRequested();
            using (SqliteConnection connection = await LocalSqliteConnection.OpenAsync(_ConnectionString, token).ConfigureAwait(false))
            {
                LocalRequestHistoryEntry? entry = null;
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM local_request_history WHERE id = $id;";
                    command.Parameters.AddWithValue("$id", id);
                    using (SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        if (await reader.ReadAsync(token).ConfigureAwait(false)) entry = ReadMain(reader);
                    }
                }

                if (entry != null) await LoadChildrenAsync(connection, entry, token).ConfigureAwait(false);
                return entry;
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string id, CancellationToken token)
        {
            if (String.IsNullOrWhiteSpace(id)) return false;
            token.ThrowIfCancellationRequested();
            using (SqliteConnection connection = await LocalSqliteConnection.OpenAsync(_ConnectionString, token).ConfigureAwait(false))
            {
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM local_request_history WHERE id = $id;";
                    command.Parameters.AddWithValue("$id", id);
                    int removed = await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    return removed > 0;
                }
            }
        }

        /// <inheritdoc />
        public async Task<int> DeleteAllAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            using (SqliteConnection connection = await LocalSqliteConnection.OpenAsync(_ConnectionString, token).ConfigureAwait(false))
            {
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM local_request_history;";
                    return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<LocalRequestHistorySummaryBucket>> SummarizeAsync(DateTime fromUtc, DateTime toUtc, int bucketMinutes, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            int safeBucketMinutes = Math.Max(1, bucketMinutes);
            List<LocalRequestHistorySummaryBucket> buckets = CreateBuckets(fromUtc, toUtc, safeBucketMinutes);
            using (SqliteConnection connection = await LocalSqliteConnection.OpenAsync(_ConnectionString, token).ConfigureAwait(false))
            {
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT received_utc, outcome FROM local_request_history WHERE received_utc >= $fromUtc AND received_utc < $toUtc;";
                    command.Parameters.AddWithValue("$fromUtc", fromUtc.ToString("O", CultureInfo.InvariantCulture));
                    command.Parameters.AddWithValue("$toUtc", toUtc.ToString("O", CultureInfo.InvariantCulture));
                    using (SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(token).ConfigureAwait(false))
                        {
                            DateTime receivedUtc = ParseDateTime(GetString(reader, "received_utc")) ?? fromUtc;
                            int index = (int)Math.Floor((receivedUtc - fromUtc).TotalMinutes / safeBucketMinutes);
                            if (index < 0 || index >= buckets.Count) continue;
                            string outcome = GetString(reader, "outcome");
                            buckets[index].Total++;
                            if (String.Equals(outcome, LocalRequestOutcomeEnum.Failure.ToString(), StringComparison.OrdinalIgnoreCase)) buckets[index].Failure++;
                            else buckets[index].Success++;
                        }
                    }
                }
            }

            return buckets;
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<UsageRecord> EnumerateServedUsageAsync(DateTime fromUtc, DateTime toUtc, [EnumeratorCancellation] CancellationToken token)
        {
            using (SqliteConnection connection = await LocalSqliteConnection.OpenAsync(_ConnectionString, token).ConfigureAwait(false))
            {
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT h.received_utc, h.model_name, h.provider_name, h.status_code, h.outcome, h.duration_ms, t.duration_ms AS t_duration, t.time_to_headers_ms, t.time_to_first_byte_ms, t.prompt_tokens, t.completion_tokens, t.total_tokens "
                        + "FROM local_request_history h LEFT JOIN local_request_telemetry t ON t.request_id = h.id WHERE h.received_utc >= $fromUtc AND h.received_utc < $toUtc;";
                    command.Parameters.AddWithValue("$fromUtc", fromUtc.ToString("O", CultureInfo.InvariantCulture));
                    command.Parameters.AddWithValue("$toUtc", toUtc.ToString("O", CultureInfo.InvariantCulture));
                    using (SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(token).ConfigureAwait(false))
                        {
                            DateTime received = ParseDateTime(GetString(reader, "received_utc")) ?? fromUtc;
                            bool failed = String.Equals(GetString(reader, "outcome"), LocalRequestOutcomeEnum.Failure.ToString(), StringComparison.OrdinalIgnoreCase);
                            double? duration = UsageDouble(reader, "t_duration") ?? UsageDouble(reader, "duration_ms");
                            long? prompt = UsageLong(reader, "prompt_tokens");
                            long? completion = UsageLong(reader, "completion_tokens");
                            if (!prompt.HasValue && !completion.HasValue) prompt = UsageLong(reader, "total_tokens");
                            long? status = UsageLong(reader, "status_code");
                            yield return UsageRecordMapper.FromServedRequest(received, NullIfEmpty(GetString(reader, "model_name")), NullIfEmpty(GetString(reader, "provider_name")), status.HasValue ? (int)status.Value : null, !failed, duration, UsageDouble(reader, "time_to_headers_ms"), UsageDouble(reader, "time_to_first_byte_ms"), prompt, completion);
                        }
                    }
                }
            }
        }

        /// <inheritdoc />
        public async Task<int> IngestLocalInferenceAsync(IEnumerable<LocalInferenceRequestRecord> records, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(records);
            int added = 0;
            using (SqliteConnection connection = await LocalSqliteConnection.OpenAsync(_ConnectionString, token).ConfigureAwait(false))
            {
                using (SqliteTransaction transaction = connection.BeginTransaction())
                {
                    foreach (LocalInferenceRequestRecord record in records)
                    {
                        if (record == null || String.IsNullOrWhiteSpace(record.Id)) continue;
                        using (SqliteCommand command = connection.CreateCommand())
                        {
                            command.Transaction = transaction;
                            command.CommandText = "INSERT OR IGNORE INTO local_inference_requests (id, received_utc, method, path, model, route, provider, reason, status_code, stream, fallback, latency_ms, first_byte_ms, queued_ms, response_bytes, prompt_tokens, completion_tokens, error) "
                                + "VALUES ($id, $received, $method, $path, $model, $route, $provider, $reason, $status, $stream, $fallback, $latency, $firstByte, $queued, $bytes, $prompt, $completion, $error);";
                            command.Parameters.AddWithValue("$id", record.Id);
                            command.Parameters.AddWithValue("$received", record.ReceivedUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
                            command.Parameters.AddWithValue("$method", record.Method ?? String.Empty);
                            command.Parameters.AddWithValue("$path", record.Path ?? String.Empty);
                            command.Parameters.AddWithValue("$model", record.Model ?? String.Empty);
                            command.Parameters.AddWithValue("$route", record.Route ?? String.Empty);
                            command.Parameters.AddWithValue("$provider", record.Provider ?? String.Empty);
                            command.Parameters.AddWithValue("$reason", (object?)record.Reason ?? DBNull.Value);
                            command.Parameters.AddWithValue("$status", record.StatusCode);
                            command.Parameters.AddWithValue("$stream", record.Stream ? 1 : 0);
                            command.Parameters.AddWithValue("$fallback", record.Fallback ? 1 : 0);
                            command.Parameters.AddWithValue("$latency", record.LatencyMs);
                            command.Parameters.AddWithValue("$firstByte", (object?)record.FirstByteMs ?? DBNull.Value);
                            command.Parameters.AddWithValue("$queued", record.QueuedMs);
                            command.Parameters.AddWithValue("$bytes", (object?)record.ResponseBytes ?? DBNull.Value);
                            command.Parameters.AddWithValue("$prompt", (object?)record.PromptTokens ?? DBNull.Value);
                            command.Parameters.AddWithValue("$completion", (object?)record.CompletionTokens ?? DBNull.Value);
                            command.Parameters.AddWithValue("$error", String.IsNullOrEmpty(record.Error) ? DBNull.Value : record.Error);
                            added += await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                        }
                    }

                    transaction.Commit();
                }
            }

            return added;
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<UsageRecord> EnumerateLocalInferenceUsageAsync(DateTime fromUtc, DateTime toUtc, [EnumeratorCancellation] CancellationToken token)
        {
            using (SqliteConnection connection = await LocalSqliteConnection.OpenAsync(_ConnectionString, token).ConfigureAwait(false))
            {
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT received_utc, model, route, provider, status_code, fallback, latency_ms, first_byte_ms, queued_ms, prompt_tokens, completion_tokens FROM local_inference_requests WHERE received_utc >= $fromUtc AND received_utc < $toUtc;";
                    command.Parameters.AddWithValue("$fromUtc", fromUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
                    command.Parameters.AddWithValue("$toUtc", toUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
                    using (SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(token).ConfigureAwait(false))
                        {
                            DateTime received = ParseDateTime(GetString(reader, "received_utc")) ?? fromUtc;
                            yield return UsageRecordMapper.FromLocalEndpointRequest(received, GetString(reader, "model"), GetString(reader, "route"), GetString(reader, "provider"), (int)(UsageLong(reader, "status_code") ?? 0), UsageLong(reader, "fallback") == 1, UsageDouble(reader, "latency_ms") ?? 0, UsageDouble(reader, "first_byte_ms"), UsageDouble(reader, "queued_ms") ?? 0, UsageLong(reader, "prompt_tokens"), null, UsageLong(reader, "completion_tokens"));
                        }
                    }
                }
            }
        }

        /// <inheritdoc />
        public async Task<int> PruneLocalInferenceAsync(DateTime cutoffUtc, CancellationToken token)
        {
            using (SqliteConnection connection = await LocalSqliteConnection.OpenAsync(_ConnectionString, token).ConfigureAwait(false))
            {
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM local_inference_requests WHERE received_utc < $cutoff;";
                    command.Parameters.AddWithValue("$cutoff", cutoffUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
                    return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }
        }

        private static double? UsageDouble(SqliteDataReader reader, string name)
        {
            int ordinal = reader.GetOrdinal(name);
            if (reader.IsDBNull(ordinal)) return null;
            return Convert.ToDouble(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
        }

        private static long? UsageLong(SqliteDataReader reader, string name)
        {
            int ordinal = reader.GetOrdinal(name);
            if (reader.IsDBNull(ordinal)) return null;
            return Convert.ToInt64(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
        }

        private static string? NullIfEmpty(string? value)
        {
            return String.IsNullOrWhiteSpace(value) ? null : value;
        }

        /// <inheritdoc />
        public async Task<LocalDashboardStatisticsResponse> StatisticsAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            LocalDashboardStatisticsResponse response = new LocalDashboardStatisticsResponse();
            using (SqliteConnection connection = await LocalSqliteConnection.OpenAsync(_ConnectionString, token).ConfigureAwait(false))
            {
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = """
SELECT
    COUNT(*) AS total_count,
    SUM(CASE WHEN outcome = 'Failure' THEN 1 ELSE 0 END) AS failure_count,
    SUM(CASE WHEN streamed = 1 THEN 1 ELSE 0 END) AS streamed_count,
    AVG(duration_ms) AS avg_duration_ms,
    MIN(received_utc) AS oldest_utc,
    MAX(received_utc) AS newest_utc
FROM local_request_history;
""";
                    using (SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        if (await reader.ReadAsync(token).ConfigureAwait(false))
                        {
                            response.Requests = GetInt32(reader, "total_count");
                            response.Failures = GetInt32(reader, "failure_count");
                            response.StreamingRequests = GetInt32(reader, "streamed_count");
                            response.AverageLatencyMs = GetNullableDouble(reader, "avg_duration_ms");
                            response.OldestRequestUtc = ParseDateTime(GetNullableString(reader, "oldest_utc"));
                            response.NewestRequestUtc = ParseDateTime(GetNullableString(reader, "newest_utc"));
                        }
                    }
                }
            }

            return response;
        }

        /// <inheritdoc />
        public async Task ReplaceProviderSnapshotsAsync(IReadOnlyList<ClientRunnerSettings> providers, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(providers);
            token.ThrowIfCancellationRequested();
            using (SqliteConnection connection = await LocalSqliteConnection.OpenAsync(_ConnectionString, token).ConfigureAwait(false))
            {
                using (SqliteTransaction transaction = connection.BeginTransaction())
                {
                    using (SqliteCommand deleteCommand = connection.CreateCommand())
                    {
                        deleteCommand.Transaction = transaction;
                        deleteCommand.CommandText = "DELETE FROM local_provider_snapshots;";
                        await deleteCommand.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }

                    foreach (ClientRunnerSettings provider in providers)
                    {
                        using (SqliteCommand command = connection.CreateCommand())
                        {
                            command.Transaction = transaction;
                            command.CommandText = """
INSERT INTO local_provider_snapshots
(
    name, api_type, endpoint, timeout_ms, model_count, captured_utc
)
VALUES
(
    $name, $apiType, $endpoint, $timeoutMs, $modelCount, $capturedUtc
);
""";
                            command.Parameters.AddWithValue("$name", provider.Name);
                            command.Parameters.AddWithValue("$apiType", provider.ApiType);
                            command.Parameters.AddWithValue("$endpoint", provider.Endpoint);
                            command.Parameters.AddWithValue("$timeoutMs", provider.TimeoutMs);
                            command.Parameters.AddWithValue("$modelCount", provider.Models.Count);
                            command.Parameters.AddWithValue("$capturedUtc", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                            await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                        }
                    }

                    await transaction.CommitAsync(token).ConfigureAwait(false);
                }
            }
        }

        /// <inheritdoc />
        public async Task ReplaceModelSnapshotsAsync(IReadOnlyList<ClientRunnerSettings> providers, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(providers);
            token.ThrowIfCancellationRequested();
            using (SqliteConnection connection = await LocalSqliteConnection.OpenAsync(_ConnectionString, token).ConfigureAwait(false))
            {
                using (SqliteTransaction transaction = connection.BeginTransaction())
                {
                    using (SqliteCommand deleteCommand = connection.CreateCommand())
                    {
                        deleteCommand.Transaction = transaction;
                        deleteCommand.CommandText = "DELETE FROM local_model_snapshots;";
                        await deleteCommand.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }

                    foreach (ClientRunnerSettings provider in providers)
                    {
                        foreach (ClientModelSettings model in provider.Models)
                        {
                            using (SqliteCommand command = connection.CreateCommand())
                            {
                                command.Transaction = transaction;
                                command.CommandText = """
INSERT INTO local_model_snapshots
(
    provider_name, api_type, model_name, supports_completions, supports_embeddings, captured_utc
)
VALUES
(
    $providerName, $apiType, $modelName, $supportsCompletions, $supportsEmbeddings, $capturedUtc
);
""";
                                command.Parameters.AddWithValue("$providerName", provider.Name);
                                command.Parameters.AddWithValue("$apiType", provider.ApiType);
                                command.Parameters.AddWithValue("$modelName", model.Name);
                                command.Parameters.AddWithValue("$supportsCompletions", model.SupportsCompletions ? 1 : 0);
                                command.Parameters.AddWithValue("$supportsEmbeddings", model.SupportsEmbeddings ? 1 : 0);
                                command.Parameters.AddWithValue("$capturedUtc", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                            }
                        }
                    }

                    await transaction.CommitAsync(token).ConfigureAwait(false);
                }
            }
        }

        /// <inheritdoc />
        public async Task SetMetadataAsync(string key, string value, CancellationToken token)
        {
            if (String.IsNullOrWhiteSpace(key)) throw new ArgumentException("Metadata key is required.", nameof(key));
            token.ThrowIfCancellationRequested();
            using (SqliteConnection connection = await LocalSqliteConnection.OpenAsync(_ConnectionString, token).ConfigureAwait(false))
            {
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = """
INSERT INTO local_dashboard_metadata (key, value, updated_utc)
VALUES ($key, $value, $updatedUtc)
ON CONFLICT(key) DO UPDATE SET
    value = excluded.value,
    updated_utc = excluded.updated_utc;
""";
                    command.Parameters.AddWithValue("$key", key);
                    command.Parameters.AddWithValue("$value", value);
                    command.Parameters.AddWithValue("$updatedUtc", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                    await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }
        }

        /// <inheritdoc />
        public async Task<string?> GetMetadataAsync(string key, CancellationToken token)
        {
            if (String.IsNullOrWhiteSpace(key)) return null;
            token.ThrowIfCancellationRequested();
            using (SqliteConnection connection = await LocalSqliteConnection.OpenAsync(_ConnectionString, token).ConfigureAwait(false))
            {
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT value FROM local_dashboard_metadata WHERE key = $key;";
                    command.Parameters.AddWithValue("$key", key);
                    object? value = await command.ExecuteScalarAsync(token).ConfigureAwait(false);
                    return value == null || value == DBNull.Value ? null : value.ToString();
                }
            }
        }

        private static async Task UpsertMainAsync(SqliteConnection connection, SqliteTransaction transaction, LocalRequestHistoryEntry entry, CancellationToken token)
        {
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
INSERT INTO local_request_history
(
    id, tenant_id, client_id, received_utc, completed_utc, method, path, requestor_ip,
    requestor_tenant_id, requestor_principal_type, requestor_principal_id,
    provider_name, api_type, model_name, provider_telemetry_sample_id,
    provider_telemetry_health_status, provider_telemetry_queued_requests,
    status_code, outcome, error, streamed, duration_ms,
    updated_utc
)
VALUES
(
    $id, $tenantId, $clientId, $receivedUtc, $completedUtc, $method, $path, $requestorIp,
    $requestorTenantId, $requestorPrincipalType, $requestorPrincipalId,
    $providerName, $apiType, $modelName, $providerTelemetrySampleId,
    $providerTelemetryHealthStatus, $providerTelemetryQueuedRequests,
    $statusCode, $outcome, $error, $streamed, $durationMs,
    $updatedUtc
)
ON CONFLICT(id) DO UPDATE SET
    tenant_id = excluded.tenant_id,
    client_id = excluded.client_id,
    completed_utc = excluded.completed_utc,
    method = excluded.method,
    path = excluded.path,
    requestor_ip = excluded.requestor_ip,
    requestor_tenant_id = excluded.requestor_tenant_id,
    requestor_principal_type = excluded.requestor_principal_type,
    requestor_principal_id = excluded.requestor_principal_id,
    provider_name = excluded.provider_name,
    api_type = excluded.api_type,
    model_name = excluded.model_name,
    provider_telemetry_sample_id = excluded.provider_telemetry_sample_id,
    provider_telemetry_health_status = excluded.provider_telemetry_health_status,
    provider_telemetry_queued_requests = excluded.provider_telemetry_queued_requests,
    status_code = excluded.status_code,
    outcome = excluded.outcome,
    error = excluded.error,
    streamed = excluded.streamed,
    duration_ms = excluded.duration_ms,
    updated_utc = excluded.updated_utc;
""";
                command.Parameters.AddWithValue("$id", entry.Id);
                command.Parameters.AddWithValue("$tenantId", DbValue(entry.TenantId));
                command.Parameters.AddWithValue("$clientId", DbValue(entry.ClientId));
                command.Parameters.AddWithValue("$receivedUtc", entry.ReceivedUtc.ToString("O", CultureInfo.InvariantCulture));
                command.Parameters.AddWithValue("$completedUtc", DbValue(entry.CompletedUtc?.ToString("O", CultureInfo.InvariantCulture)));
                command.Parameters.AddWithValue("$method", entry.Method);
                command.Parameters.AddWithValue("$path", entry.Path);
                command.Parameters.AddWithValue("$requestorIp", DbValue(entry.RequestorIp));
                command.Parameters.AddWithValue("$requestorTenantId", DbValue(entry.RequestorTenantId));
                command.Parameters.AddWithValue("$requestorPrincipalType", DbValue(entry.RequestorPrincipalType));
                command.Parameters.AddWithValue("$requestorPrincipalId", DbValue(entry.RequestorPrincipalId));
                command.Parameters.AddWithValue("$providerName", DbValue(entry.ProviderName));
                command.Parameters.AddWithValue("$apiType", DbValue(entry.ApiType));
                command.Parameters.AddWithValue("$modelName", DbValue(entry.ModelName));
                command.Parameters.AddWithValue("$providerTelemetrySampleId", DbValue(entry.ProviderTelemetrySampleId));
                command.Parameters.AddWithValue("$providerTelemetryHealthStatus", DbValue(entry.ProviderTelemetryHealthStatus));
                command.Parameters.AddWithValue("$providerTelemetryQueuedRequests", entry.ProviderTelemetryQueuedRequests.HasValue ? entry.ProviderTelemetryQueuedRequests.Value : DBNull.Value);
                command.Parameters.AddWithValue("$statusCode", entry.StatusCode.HasValue ? entry.StatusCode.Value : DBNull.Value);
                command.Parameters.AddWithValue("$outcome", entry.Outcome.ToString());
                command.Parameters.AddWithValue("$error", DbValue(entry.Error));
                command.Parameters.AddWithValue("$streamed", entry.Streamed ? 1 : 0);
                command.Parameters.AddWithValue("$durationMs", entry.Telemetry.DurationMs.HasValue ? entry.Telemetry.DurationMs.Value : DBNull.Value);
                command.Parameters.AddWithValue("$updatedUtc", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }

        private static async Task DeleteChildrenAsync(SqliteConnection connection, SqliteTransaction transaction, string id, CancellationToken token)
        {
            foreach (string table in ChildTables())
            {
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = "DELETE FROM " + Sanitizer.Table(table) + " WHERE request_id = $requestId;";
                    command.Parameters.AddWithValue("$requestId", id);
                    await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }
        }

        private static async Task InsertHeadersAsync(SqliteConnection connection, SqliteTransaction transaction, string table, string requestId, Dictionary<string, string> headers, CancellationToken token)
        {
            Dictionary<string, string> normalized = NormalizeHeaders(headers);
            foreach (KeyValuePair<string, string> header in normalized)
            {
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = "INSERT INTO " + Sanitizer.Table(table) + " (request_id, name, value) VALUES ($requestId, $name, $value);";
                    command.Parameters.AddWithValue("$requestId", requestId);
                    command.Parameters.AddWithValue("$name", header.Key);
                    command.Parameters.AddWithValue("$value", header.Value);
                    await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }
        }

        private static Dictionary<string, string> NormalizeHeaders(Dictionary<string, string>? headers)
        {
            Dictionary<string, string> normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (headers == null) return normalized;
            foreach (KeyValuePair<string, string> header in headers)
            {
                string name = CanonicalizeHeaderName(header.Key);
                if (String.IsNullOrWhiteSpace(name)) continue;
                normalized[name] = header.Value;
            }

            return normalized;
        }

        private static string CanonicalizeHeaderName(string name)
        {
            if (String.IsNullOrWhiteSpace(name)) return String.Empty;
            if (String.Equals(name, "x-forwarded-for", StringComparison.OrdinalIgnoreCase)) return "X-Forwarded-For";
            if (String.Equals(name, "x-real-ip", StringComparison.OrdinalIgnoreCase)) return "X-Real-IP";
            if (String.Equals(name, "x-xeno-request-id", StringComparison.OrdinalIgnoreCase)) return "X-Xeno-Request-ID";
            if (String.Equals(name, "x-xeno-requestor-ip", StringComparison.OrdinalIgnoreCase)) return "X-Xeno-Requestor-IP";
            if (String.Equals(name, "x-xeno-tenant-id", StringComparison.OrdinalIgnoreCase)) return "X-Xeno-Tenant-ID";
            if (String.Equals(name, "content-type", StringComparison.OrdinalIgnoreCase)) return "Content-Type";
            if (String.Equals(name, "authorization", StringComparison.OrdinalIgnoreCase)) return "Authorization";
            return name.Trim();
        }

        private static async Task InsertBodyAsync(SqliteConnection connection, SqliteTransaction transaction, string table, string requestId, string? body, CancellationToken token)
        {
            if (body == null) return;
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "INSERT INTO " + Sanitizer.Table(table) + " (request_id, body, byte_count) VALUES ($requestId, $body, $byteCount);";
                command.Parameters.AddWithValue("$requestId", requestId);
                command.Parameters.AddWithValue("$body", body);
                command.Parameters.AddWithValue("$byteCount", Encoding.UTF8.GetByteCount(body));
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }

        private static async Task InsertTelemetryAsync(SqliteConnection connection, SqliteTransaction transaction, string requestId, LocalRequestTelemetry telemetry, CancellationToken token)
        {
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
INSERT INTO local_request_telemetry
(
    request_id, duration_ms, time_to_headers_ms, time_to_first_byte_ms, total_tokens,
    prompt_tokens, completion_tokens, stream_chunk_count, stream_event_count
)
VALUES
(
    $requestId, $durationMs, $timeToHeadersMs, $timeToFirstByteMs, $totalTokens,
    $promptTokens, $completionTokens, $streamChunkCount, $streamEventCount
);
""";
                command.Parameters.AddWithValue("$requestId", requestId);
                command.Parameters.AddWithValue("$durationMs", telemetry.DurationMs.HasValue ? telemetry.DurationMs.Value : DBNull.Value);
                command.Parameters.AddWithValue("$timeToHeadersMs", telemetry.TimeToHeadersMs.HasValue ? telemetry.TimeToHeadersMs.Value : DBNull.Value);
                command.Parameters.AddWithValue("$timeToFirstByteMs", telemetry.TimeToFirstByteMs.HasValue ? telemetry.TimeToFirstByteMs.Value : DBNull.Value);
                command.Parameters.AddWithValue("$totalTokens", telemetry.TotalTokens.HasValue ? telemetry.TotalTokens.Value : DBNull.Value);
                command.Parameters.AddWithValue("$promptTokens", telemetry.PromptTokens.HasValue ? telemetry.PromptTokens.Value : DBNull.Value);
                command.Parameters.AddWithValue("$completionTokens", telemetry.CompletionTokens.HasValue ? telemetry.CompletionTokens.Value : DBNull.Value);
                command.Parameters.AddWithValue("$streamChunkCount", telemetry.StreamChunkCount);
                command.Parameters.AddWithValue("$streamEventCount", telemetry.StreamEventCount);
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }

        private static async Task InsertTimelineAsync(SqliteConnection connection, SqliteTransaction transaction, string requestId, List<LocalRequestTimelineEvent> timeline, CancellationToken token)
        {
            int sequence = 0;
            foreach (LocalRequestTimelineEvent item in timeline)
            {
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = "INSERT INTO local_request_timeline (request_id, sequence, stage, timestamp_utc, message) VALUES ($requestId, $sequence, $stage, $timestampUtc, $message);";
                    command.Parameters.AddWithValue("$requestId", requestId);
                    command.Parameters.AddWithValue("$sequence", sequence);
                    command.Parameters.AddWithValue("$stage", item.Stage.ToString());
                    command.Parameters.AddWithValue("$timestampUtc", item.TimestampUtc.ToString("O", CultureInfo.InvariantCulture));
                    command.Parameters.AddWithValue("$message", DbValue(item.Message));
                    await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }

                sequence++;
            }
        }

        private static async Task PruneAsync(SqliteConnection connection, SqliteTransaction transaction, int maxEntries, CancellationToken token)
        {
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
DELETE FROM local_request_history
WHERE id IN
(
    SELECT id FROM local_request_history
    ORDER BY received_utc DESC
    LIMIT -1 OFFSET $maxEntries
);
""";
                command.Parameters.AddWithValue("$maxEntries", maxEntries);
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }

        private static async Task LoadChildrenAsync(SqliteConnection connection, LocalRequestHistoryEntry entry, CancellationToken token)
        {
            entry.RequestHeaders = await LoadHeadersAsync(connection, "local_request_headers", entry.Id, token).ConfigureAwait(false);
            entry.ResponseHeaders = await LoadHeadersAsync(connection, "local_response_headers", entry.Id, token).ConfigureAwait(false);
            entry.RequestBody = await LoadBodyAsync(connection, "local_request_bodies", entry.Id, token).ConfigureAwait(false);
            entry.ResponseBody = await LoadBodyAsync(connection, "local_response_bodies", entry.Id, token).ConfigureAwait(false);
            entry.Telemetry = await LoadTelemetryAsync(connection, entry.Id, token).ConfigureAwait(false);
            entry.Timeline = await LoadTimelineAsync(connection, entry.Id, token).ConfigureAwait(false);
        }

        private static async Task<Dictionary<string, string>> LoadHeadersAsync(SqliteConnection connection, string table, string requestId, CancellationToken token)
        {
            Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT name, value FROM " + Sanitizer.Table(table) + " WHERE request_id = $requestId ORDER BY name;";
                command.Parameters.AddWithValue("$requestId", requestId);
                using (SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        headers[GetString(reader, "name")] = GetString(reader, "value");
                    }
                }
            }

            return headers;
        }

        private static async Task<string?> LoadBodyAsync(SqliteConnection connection, string table, string requestId, CancellationToken token)
        {
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT body FROM " + Sanitizer.Table(table) + " WHERE request_id = $requestId LIMIT 1;";
                command.Parameters.AddWithValue("$requestId", requestId);
                object? value = await command.ExecuteScalarAsync(token).ConfigureAwait(false);
                return value == null || value == DBNull.Value ? null : value.ToString();
            }
        }

        private static async Task<LocalRequestTelemetry> LoadTelemetryAsync(SqliteConnection connection, string requestId, CancellationToken token)
        {
            LocalRequestTelemetry telemetry = new LocalRequestTelemetry();
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT * FROM local_request_telemetry WHERE request_id = $requestId LIMIT 1;";
                command.Parameters.AddWithValue("$requestId", requestId);
                using (SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    if (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        telemetry.DurationMs = GetNullableDouble(reader, "duration_ms");
                        telemetry.TimeToHeadersMs = GetNullableDouble(reader, "time_to_headers_ms");
                        telemetry.TimeToFirstByteMs = GetNullableDouble(reader, "time_to_first_byte_ms");
                        telemetry.TotalTokens = GetNullableInt32(reader, "total_tokens");
                        telemetry.PromptTokens = GetNullableInt32(reader, "prompt_tokens");
                        telemetry.CompletionTokens = GetNullableInt32(reader, "completion_tokens");
                        telemetry.StreamChunkCount = GetInt32(reader, "stream_chunk_count");
                        telemetry.StreamEventCount = GetInt32(reader, "stream_event_count");
                    }
                }
            }

            return telemetry;
        }

        private static async Task<List<LocalRequestTimelineEvent>> LoadTimelineAsync(SqliteConnection connection, string requestId, CancellationToken token)
        {
            List<LocalRequestTimelineEvent> timeline = new List<LocalRequestTimelineEvent>();
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT stage, timestamp_utc, message FROM local_request_timeline WHERE request_id = $requestId ORDER BY sequence;";
                command.Parameters.AddWithValue("$requestId", requestId);
                using (SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        LocalRequestTimelineStageEnum stage = LocalRequestTimelineStageEnum.Received;
                        Enum.TryParse(GetString(reader, "stage"), true, out stage);
                        timeline.Add(new LocalRequestTimelineEvent
                        {
                            Stage = stage,
                            TimestampUtc = ParseDateTime(GetString(reader, "timestamp_utc")) ?? DateTime.UtcNow,
                            Message = GetNullableString(reader, "message")
                        });
                    }
                }
            }

            return timeline;
        }

        private static LocalRequestHistoryEntry ReadMain(IDataRecord reader)
        {
            LocalRequestOutcomeEnum outcome = LocalRequestOutcomeEnum.Processing;
            Enum.TryParse(GetString(reader, "outcome"), true, out outcome);
            LocalRequestHistoryEntry entry = new LocalRequestHistoryEntry
            {
                Id = GetString(reader, "id"),
                TenantId = GetNullableString(reader, "tenant_id"),
                ClientId = GetNullableString(reader, "client_id"),
                ReceivedUtc = ParseDateTime(GetString(reader, "received_utc")) ?? DateTime.UtcNow,
                CompletedUtc = ParseDateTime(GetNullableString(reader, "completed_utc")),
                Method = GetString(reader, "method"),
                Path = GetString(reader, "path"),
                RequestorIp = GetNullableString(reader, "requestor_ip"),
                RequestorTenantId = GetNullableString(reader, "requestor_tenant_id"),
                RequestorPrincipalType = GetNullableString(reader, "requestor_principal_type"),
                RequestorPrincipalId = GetNullableString(reader, "requestor_principal_id"),
                ProviderName = GetNullableString(reader, "provider_name"),
                ApiType = GetNullableString(reader, "api_type"),
                ModelName = GetNullableString(reader, "model_name"),
                ProviderTelemetrySampleId = GetNullableString(reader, "provider_telemetry_sample_id"),
                ProviderTelemetryHealthStatus = GetNullableString(reader, "provider_telemetry_health_status"),
                ProviderTelemetryQueuedRequests = GetNullableDouble(reader, "provider_telemetry_queued_requests"),
                StatusCode = GetNullableInt32(reader, "status_code"),
                Outcome = outcome,
                Error = GetNullableString(reader, "error"),
                Streamed = GetInt32(reader, "streamed") == 1
            };
            entry.Telemetry.DurationMs = GetNullableDouble(reader, "duration_ms");
            return entry;
        }

        private static List<LocalRequestHistorySummaryBucket> CreateBuckets(DateTime fromUtc, DateTime toUtc, int bucketMinutes)
        {
            List<LocalRequestHistorySummaryBucket> buckets = new List<LocalRequestHistorySummaryBucket>();
            DateTime cursor = fromUtc;
            while (cursor < toUtc)
            {
                buckets.Add(new LocalRequestHistorySummaryBucket { StartUtc = cursor });
                cursor = cursor.AddMinutes(bucketMinutes);
            }

            return buckets;
        }

        private static string[] ChildTables()
        {
            return new[]
            {
                "local_request_headers",
                "local_response_headers",
                "local_request_bodies",
                "local_response_bodies",
                "local_request_telemetry",
                "local_request_timeline"
            };
        }

        private static string? TruncateUtf8(string? value, int maxBytes)
        {
            if (value == null) return null;
            if (maxBytes <= 0) return string.Empty;
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            if (bytes.Length <= maxBytes) return value;
            return Encoding.UTF8.GetString(bytes, 0, maxBytes);
        }

        private static object DbValue(string? value)
        {
            return String.IsNullOrWhiteSpace(value) ? DBNull.Value : value;
        }

        private static string GetString(IDataRecord reader, string name)
        {
            int ordinal = reader.GetOrdinal(name);
            object value = reader.GetValue(ordinal);
            return value == DBNull.Value ? string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private static string? GetNullableString(IDataRecord reader, string name)
        {
            int ordinal = reader.GetOrdinal(name);
            object value = reader.GetValue(ordinal);
            return value == DBNull.Value ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static int GetInt32(IDataRecord reader, string name)
        {
            int ordinal = reader.GetOrdinal(name);
            object value = reader.GetValue(ordinal);
            return value == DBNull.Value ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        private static int? GetNullableInt32(IDataRecord reader, string name)
        {
            int ordinal = reader.GetOrdinal(name);
            object value = reader.GetValue(ordinal);
            return value == DBNull.Value ? null : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        private static double? GetNullableDouble(IDataRecord reader, string name)
        {
            int ordinal = reader.GetOrdinal(name);
            object value = reader.GetValue(ordinal);
            return value == DBNull.Value ? null : Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }

        private static DateTime? ParseDateTime(string? value)
        {
            if (String.IsNullOrWhiteSpace(value)) return null;
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime parsed)) return parsed.ToUniversalTime();
            return null;
        }

        private static string SchemaSql()
        {
            return """
CREATE TABLE IF NOT EXISTS schema_migrations
(
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    applied_utc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS local_request_history
(
    id TEXT PRIMARY KEY,
    tenant_id TEXT NULL,
    client_id TEXT NULL,
    received_utc TEXT NOT NULL,
    completed_utc TEXT NULL,
    method TEXT NOT NULL,
    path TEXT NOT NULL,
    requestor_ip TEXT NULL,
    requestor_tenant_id TEXT NULL,
    requestor_principal_type TEXT NULL,
    requestor_principal_id TEXT NULL,
    provider_name TEXT NULL,
    api_type TEXT NULL,
    model_name TEXT NULL,
    provider_telemetry_sample_id TEXT NULL,
    provider_telemetry_health_status TEXT NULL,
    provider_telemetry_queued_requests REAL NULL,
    status_code INTEGER NULL,
    outcome TEXT NOT NULL,
    error TEXT NULL,
    streamed INTEGER NOT NULL DEFAULT 0,
    duration_ms REAL NULL,
    updated_utc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS local_request_headers
(
    request_id TEXT NOT NULL,
    name TEXT NOT NULL,
    value TEXT NOT NULL,
    FOREIGN KEY(request_id) REFERENCES local_request_history(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS local_response_headers
(
    request_id TEXT NOT NULL,
    name TEXT NOT NULL,
    value TEXT NOT NULL,
    FOREIGN KEY(request_id) REFERENCES local_request_history(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS local_request_bodies
(
    request_id TEXT PRIMARY KEY,
    body TEXT NOT NULL,
    byte_count INTEGER NOT NULL,
    FOREIGN KEY(request_id) REFERENCES local_request_history(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS local_response_bodies
(
    request_id TEXT PRIMARY KEY,
    body TEXT NOT NULL,
    byte_count INTEGER NOT NULL,
    FOREIGN KEY(request_id) REFERENCES local_request_history(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS local_request_telemetry
(
    request_id TEXT PRIMARY KEY,
    duration_ms REAL NULL,
    time_to_headers_ms REAL NULL,
    time_to_first_byte_ms REAL NULL,
    total_tokens INTEGER NULL,
    prompt_tokens INTEGER NULL,
    completion_tokens INTEGER NULL,
    stream_chunk_count INTEGER NOT NULL DEFAULT 0,
    stream_event_count INTEGER NOT NULL DEFAULT 0,
    FOREIGN KEY(request_id) REFERENCES local_request_history(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS local_request_timeline
(
    request_id TEXT NOT NULL,
    sequence INTEGER NOT NULL,
    stage TEXT NOT NULL,
    timestamp_utc TEXT NOT NULL,
    message TEXT NULL,
    FOREIGN KEY(request_id) REFERENCES local_request_history(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS local_provider_snapshots
(
    name TEXT NOT NULL,
    api_type TEXT NOT NULL,
    endpoint TEXT NOT NULL,
    timeout_ms INTEGER NOT NULL,
    model_count INTEGER NOT NULL,
    captured_utc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS local_model_snapshots
(
    provider_name TEXT NOT NULL,
    api_type TEXT NOT NULL,
    model_name TEXT NOT NULL,
    supports_completions INTEGER NOT NULL,
    supports_embeddings INTEGER NOT NULL,
    captured_utc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS local_dashboard_metadata
(
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL,
    updated_utc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS local_inference_requests
(
    id TEXT PRIMARY KEY,
    received_utc TEXT NOT NULL,
    method TEXT NOT NULL,
    path TEXT NOT NULL,
    model TEXT NOT NULL,
    route TEXT NOT NULL,
    provider TEXT NOT NULL,
    reason TEXT NULL,
    status_code INTEGER NOT NULL,
    stream INTEGER NOT NULL DEFAULT 0,
    fallback INTEGER NOT NULL DEFAULT 0,
    latency_ms REAL NOT NULL,
    first_byte_ms REAL NULL,
    queued_ms REAL NOT NULL DEFAULT 0,
    response_bytes INTEGER NULL,
    prompt_tokens INTEGER NULL,
    completion_tokens INTEGER NULL,
    error TEXT NULL
);

CREATE INDEX IF NOT EXISTS idx_local_inference_requests_received ON local_inference_requests(received_utc);
CREATE INDEX IF NOT EXISTS idx_local_request_history_received ON local_request_history(received_utc);
CREATE INDEX IF NOT EXISTS idx_local_request_history_completed ON local_request_history(completed_utc);
CREATE INDEX IF NOT EXISTS idx_local_request_history_model ON local_request_history(model_name);
CREATE INDEX IF NOT EXISTS idx_local_request_history_provider ON local_request_history(provider_name);
CREATE INDEX IF NOT EXISTS idx_local_request_history_outcome ON local_request_history(outcome);
CREATE INDEX IF NOT EXISTS idx_local_request_history_path ON local_request_history(path);
CREATE INDEX IF NOT EXISTS idx_local_request_history_requestor ON local_request_history(requestor_ip);
CREATE INDEX IF NOT EXISTS idx_local_provider_snapshots_name ON local_provider_snapshots(name);
CREATE INDEX IF NOT EXISTS idx_local_model_snapshots_model ON local_model_snapshots(model_name);
CREATE INDEX IF NOT EXISTS idx_local_model_snapshots_provider ON local_model_snapshots(provider_name);

INSERT OR IGNORE INTO schema_migrations (id, name, applied_utc)
VALUES ('0001', 'local_request_history', strftime('%Y-%m-%dT%H:%M:%fZ', 'now'));
INSERT OR IGNORE INTO schema_migrations (id, name, applied_utc)
VALUES ('0002', 'local_snapshots_metadata', strftime('%Y-%m-%dT%H:%M:%fZ', 'now'));
""";
        }
    }
}

namespace Xeno.Client.LocalDashboard.Database
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Sqlite;
    using SQLitePCL;
    using Xeno.Client.Telemetry.Models;
    using Xeno.Core.Helpers;
    using Xeno.Core.Serialization;

    /// <summary>
    /// SQLite provider telemetry history methods.
    /// </summary>
    public sealed class SqliteClientProviderTelemetryHistoryMethods : IClientProviderTelemetryHistoryMethods
    {
        private readonly string _ConnectionString;

        /// <summary>
        /// Instantiate SQLite provider telemetry history methods.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        public SqliteClientProviderTelemetryHistoryMethods(ClientLocalDatabaseSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            Batteries_V2.Init();
            string resolvedFilename = settings.ResolveFilename();
            Directory.CreateDirectory(Path.GetDirectoryName(resolvedFilename) ?? ClientSettings.SettingsDirectory);
            _ConnectionString = String.IsNullOrWhiteSpace(settings.ConnectionString)
                ? new SqliteConnectionStringBuilder { DataSource = resolvedFilename, Pooling = false, ForeignKeys = true }.ToString()
                : settings.ConnectionString;
        }

        /// <inheritdoc />
        public async Task InitializeAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            using (SqliteConnection connection = new SqliteConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = SchemaSql();
                    await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }

                await EnsureProcessColumnsAsync(connection, token).ConfigureAwait(false);
            }
        }

        private static async Task EnsureProcessColumnsAsync(SqliteConnection connection, CancellationToken token)
        {
            await TryAlterAsync(connection, "ALTER TABLE local_provider_telemetry_samples ADD COLUMN process_id INTEGER NULL;", token).ConfigureAwait(false);
            await TryAlterAsync(connection, "ALTER TABLE local_provider_telemetry_samples ADD COLUMN process_name TEXT NULL;", token).ConfigureAwait(false);
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
        public async Task<IReadOnlyList<ClientProviderTelemetrySampleRecord>> CreateAsync(ClientProviderTelemetrySnapshot snapshot, ClientTelemetrySettings settings, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            ArgumentNullException.ThrowIfNull(settings);
            token.ThrowIfCancellationRequested();
            List<ClientProviderTelemetrySampleRecord> records = new List<ClientProviderTelemetrySampleRecord>();
            using (SqliteConnection connection = new SqliteConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqliteTransaction transaction = connection.BeginTransaction())
                {
                    foreach (ClientProviderTelemetryProviderStatus provider in snapshot.Providers)
                    {
                        ClientProviderTelemetrySampleRecord record = BuildRecord(snapshot, provider);
                        records.Add(record);
                        using (SqliteCommand command = connection.CreateCommand())
                        {
                            command.Transaction = transaction;
                            command.CommandText = InsertSampleSql();
                            AddSampleParameters(command, record, settings.ProviderTelemetry.PersistRawMetrics ? JsonUtil.Serialize(provider, true) : null, provider);
                            await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                        }

                        foreach (ClientProviderTelemetryModel model in provider.Models)
                        {
                            using (SqliteCommand command = connection.CreateCommand())
                            {
                                command.Transaction = transaction;
                                command.CommandText = InsertModelSql();
                                AddModelParameters(command, record, model);
                                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                            }
                        }

                        foreach (ClientProviderTelemetryMetric metric in provider.Metrics)
                        {
                            using (SqliteCommand command = connection.CreateCommand())
                            {
                                command.Transaction = transaction;
                                command.CommandText = InsertMetricSql();
                                AddMetricParameters(command, record, metric);
                                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                            }
                        }
                    }

                    transaction.Commit();
                }
            }

            return records;
        }

        /// <inheritdoc />
        public async Task<ClientProviderTelemetrySampleDetail?> ReadLatestAsync(string? providerName, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            using (SqliteConnection connection = new SqliteConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = String.IsNullOrWhiteSpace(providerName)
                        ? "SELECT * FROM local_provider_telemetry_samples ORDER BY collected_utc DESC LIMIT 1;"
                        : "SELECT * FROM local_provider_telemetry_samples WHERE provider_name = $providerName ORDER BY collected_utc DESC LIMIT 1;";
                    if (!String.IsNullOrWhiteSpace(providerName)) command.Parameters.AddWithValue("$providerName", providerName);
                    return await ReadDetailFromCommandAsync(connection, command, token).ConfigureAwait(false);
                }
            }
        }

        /// <inheritdoc />
        public async Task<ClientProviderTelemetrySampleDetail?> ReadDetailAsync(string id, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            using (SqliteConnection connection = new SqliteConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM local_provider_telemetry_samples WHERE id = $id LIMIT 1;";
                    command.Parameters.AddWithValue("$id", id);
                    return await ReadDetailFromCommandAsync(connection, command, token).ConfigureAwait(false);
                }
            }
        }

        /// <inheritdoc />
        public async Task<ClientProviderTelemetrySearchResult> SearchAsync(ClientProviderTelemetrySearchRequest request, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(request);
            token.ThrowIfCancellationRequested();
            DateTime toUtc = request.ToUtc ?? DateTime.UtcNow;
            DateTime fromUtc = request.FromUtc ?? toUtc.AddHours(-1);
            string metricColumn = MetricColumn(request.Metric);
            ClientProviderTelemetrySearchResult result = new ClientProviderTelemetrySearchResult
            {
                Limit = Math.Clamp(request.Limit, 1, 1000),
                Offset = Math.Max(0, request.Offset)
            };
            using (SqliteConnection connection = new SqliteConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqliteCommand countCommand = connection.CreateCommand())
                {
                    countCommand.CommandText = SearchSql("COUNT(*)", metricColumn, request, false);
                    AddSearchParameters(countCommand, fromUtc, toUtc, request);
                    object? scalar = await countCommand.ExecuteScalarAsync(token).ConfigureAwait(false);
                    result.Total = Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
                }

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = SearchSql("*", metricColumn, request, true);
                    AddSearchParameters(command, fromUtc, toUtc, request);
                    command.Parameters.AddWithValue("$limit", result.Limit);
                    command.Parameters.AddWithValue("$offset", result.Offset);
                    using (SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(token).ConfigureAwait(false))
                        {
                            result.Samples.Add(ReadSample(reader));
                        }
                    }
                }
            }

            return result;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<ClientProviderTelemetryRollupBucket>> RollupAsync(ClientProviderTelemetryRollupRequest request, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(request);
            token.ThrowIfCancellationRequested();
            DateTime toUtc = request.ToUtc ?? DateTime.UtcNow;
            DateTime fromUtc = request.FromUtc ?? toUtc.AddHours(-1);
            int bucketMinutes = Math.Clamp(request.BucketMinutes, 1, 1440);
            string metricColumn = MetricColumn(request.Metric);
            List<ClientProviderTelemetryRollupBucket> buckets = BuildEmptyBuckets(fromUtc, toUtc, bucketMinutes);
            using (SqliteConnection connection = new SqliteConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = RollupSql(metricColumn, request);
                    AddRollupParameters(command, fromUtc, toUtc, request);
                    using (SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(token).ConfigureAwait(false))
                        {
                            DateTime collectedUtc = DateTime.Parse(GetString(reader, "collected_utc"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                            double metric = GetDouble(reader, "metric");
                            int index = Math.Clamp((int)Math.Floor((collectedUtc - fromUtc).TotalMinutes / bucketMinutes), 0, buckets.Count - 1);
                            ApplyBucketValue(buckets[index], metric);
                        }
                    }
                }
            }

            return buckets;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string id, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            using (SqliteConnection connection = new SqliteConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM local_provider_telemetry_samples WHERE id = $id;";
                    command.Parameters.AddWithValue("$id", id);
                    return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false) > 0;
                }
            }
        }

        /// <inheritdoc />
        public async Task<int> DeleteAllAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            using (SqliteConnection connection = new SqliteConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM local_provider_telemetry_samples;";
                    return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }
        }

        /// <inheritdoc />
        public async Task<int> DeleteOlderThanAsync(DateTime olderThanUtc, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            using (SqliteConnection connection = new SqliteConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM local_provider_telemetry_samples WHERE collected_utc < $olderThanUtc;";
                    command.Parameters.AddWithValue("$olderThanUtc", olderThanUtc.ToString("O", CultureInfo.InvariantCulture));
                    return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }
        }

        /// <inheritdoc />
        public async Task<ClientTelemetryPersistenceStatus> GetStatusAsync(ClientTelemetrySettings settings, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(settings);
            token.ThrowIfCancellationRequested();
            ClientTelemetryPersistenceStatus status = new ClientTelemetryPersistenceStatus
            {
                Enabled = settings.Enabled && settings.ProviderTelemetry.Enabled,
                CollectionIntervalMs = settings.ProviderTelemetry.CollectionIntervalMs,
                RetentionDays = settings.RetentionDays
            };
            using (SqliteConnection connection = new SqliteConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT COUNT(*) AS samples, MIN(collected_utc) AS oldest, MAX(collected_utc) AS newest FROM local_provider_telemetry_samples;";
                    using (SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        if (await reader.ReadAsync(token).ConfigureAwait(false))
                        {
                            status.Samples = GetInt32(reader, "samples");
                            status.OldestSampleUtc = GetNullableDateTime(reader, "oldest");
                            status.NewestSampleUtc = GetNullableDateTime(reader, "newest");
                        }
                    }
                }
            }

            return status;
        }

        private static async Task<ClientProviderTelemetrySampleDetail?> ReadDetailFromCommandAsync(SqliteConnection connection, SqliteCommand command, CancellationToken token)
        {
            using (SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
            {
                if (!await reader.ReadAsync(token).ConfigureAwait(false)) return null;
                ClientProviderTelemetrySampleDetail detail = new ClientProviderTelemetrySampleDetail
                {
                    Sample = ReadSample(reader)
                };
                string? snapshotJson = GetNullableString(reader, "snapshot_json");
                if (!String.IsNullOrWhiteSpace(snapshotJson))
                {
                    detail.Snapshot = JsonUtil.Deserialize<ClientProviderTelemetryProviderStatus>(snapshotJson);
                }

                reader.Close();
                detail.Models = await ReadModelsAsync(connection, detail.Sample.Id, token).ConfigureAwait(false);
                detail.Metrics = await ReadMetricsAsync(connection, detail.Sample.Id, token).ConfigureAwait(false);
                return detail;
            }
        }

        private static ClientProviderTelemetrySampleRecord BuildRecord(ClientProviderTelemetrySnapshot snapshot, ClientProviderTelemetryProviderStatus provider)
        {
            return new ClientProviderTelemetrySampleRecord
            {
                Id = IdGenerator.NewId("ptel_"),
                ClientId = snapshot.Metadata.ClientId,
                ProviderName = provider.Endpoint.ProviderName,
                ApiType = provider.Endpoint.ApiType,
                ProviderKind = provider.Endpoint.Kind,
                EndpointHash = provider.Endpoint.EndpointHash,
                EndpointDisplay = provider.Endpoint.EndpointDisplay,
                CollectedUtc = provider.CollectedUtc,
                PersistedUtc = DateTime.UtcNow,
                Status = provider.Status,
                HealthStatus = provider.HealthStatus,
                Version = provider.Version,
                AvailableModelCount = provider.AvailableModelCount,
                LoadedModelCount = provider.LoadedModelCount,
                ActiveRequests = provider.ActiveRequests,
                QueuedRequests = provider.QueuedRequests,
                WaitingRequests = provider.WaitingRequests,
                PromptTokensPerSecond = provider.PromptTokensPerSecond,
                GenerationTokensPerSecond = provider.GenerationTokensPerSecond,
                TotalTokensPerSecond = provider.TotalTokensPerSecond,
                RequestRatePerSecond = provider.RequestRatePerSecond,
                ErrorRatePerSecond = provider.ErrorRatePerSecond,
                LatencyP95Ms = provider.LatencyP95Ms,
                TimeToFirstTokenP95Ms = provider.TimeToFirstTokenP95Ms,
                CacheUsagePercent = provider.CacheUsagePercent,
                ProcessId = provider.ProcessId,
                ProcessName = provider.ProcessName,
                LastError = provider.LastError
            };
        }

        private static void AddSampleParameters(SqliteCommand command, ClientProviderTelemetrySampleRecord record, string? snapshotJson, ClientProviderTelemetryProviderStatus provider)
        {
            command.Parameters.AddWithValue("$id", record.Id);
            command.Parameters.AddWithValue("$clientId", (object?)record.ClientId ?? DBNull.Value);
            command.Parameters.AddWithValue("$providerName", record.ProviderName);
            command.Parameters.AddWithValue("$apiType", record.ApiType);
            command.Parameters.AddWithValue("$providerKind", record.ProviderKind.ToString());
            command.Parameters.AddWithValue("$endpointHash", record.EndpointHash);
            command.Parameters.AddWithValue("$endpointDisplay", record.EndpointDisplay);
            command.Parameters.AddWithValue("$collectedUtc", record.CollectedUtc.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$persistedUtc", record.PersistedUtc.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$status", record.Status.ToString());
            command.Parameters.AddWithValue("$healthStatus", record.HealthStatus.ToString());
            command.Parameters.AddWithValue("$version", (object?)record.Version ?? DBNull.Value);
            command.Parameters.AddWithValue("$availableModelCount", record.AvailableModelCount);
            command.Parameters.AddWithValue("$loadedModelCount", record.LoadedModelCount);
            command.Parameters.AddWithValue("$activeRequests", NullableDouble(record.ActiveRequests));
            command.Parameters.AddWithValue("$queuedRequests", NullableDouble(record.QueuedRequests));
            command.Parameters.AddWithValue("$waitingRequests", NullableDouble(record.WaitingRequests));
            command.Parameters.AddWithValue("$swappedRequests", NullableDouble(provider.SwappedRequests));
            command.Parameters.AddWithValue("$promptTokensPerSecond", NullableDouble(record.PromptTokensPerSecond));
            command.Parameters.AddWithValue("$generationTokensPerSecond", NullableDouble(record.GenerationTokensPerSecond));
            command.Parameters.AddWithValue("$totalTokensPerSecond", NullableDouble(record.TotalTokensPerSecond));
            command.Parameters.AddWithValue("$requestRatePerSecond", NullableDouble(record.RequestRatePerSecond));
            command.Parameters.AddWithValue("$errorRatePerSecond", NullableDouble(record.ErrorRatePerSecond));
            command.Parameters.AddWithValue("$latencyP50Ms", NullableDouble(provider.LatencyP50Ms));
            command.Parameters.AddWithValue("$latencyP95Ms", NullableDouble(record.LatencyP95Ms));
            command.Parameters.AddWithValue("$latencyP99Ms", NullableDouble(provider.LatencyP99Ms));
            command.Parameters.AddWithValue("$timeToFirstTokenP50Ms", NullableDouble(provider.TimeToFirstTokenP50Ms));
            command.Parameters.AddWithValue("$timeToFirstTokenP95Ms", NullableDouble(record.TimeToFirstTokenP95Ms));
            command.Parameters.AddWithValue("$cacheUsagePercent", NullableDouble(record.CacheUsagePercent));
            command.Parameters.AddWithValue("$gpuCacheUsagePercent", NullableDouble(provider.GpuCacheUsagePercent));
            command.Parameters.AddWithValue("$cpuCacheUsagePercent", NullableDouble(provider.CpuCacheUsagePercent));
            command.Parameters.AddWithValue("$memoryUsedBytes", NullableLong(provider.MemoryUsedBytes));
            command.Parameters.AddWithValue("$memoryTotalBytes", NullableLong(provider.MemoryTotalBytes));
            command.Parameters.AddWithValue("$processId", NullableInt(provider.ProcessId));
            command.Parameters.AddWithValue("$processName", (object?)provider.ProcessName ?? DBNull.Value);
            command.Parameters.AddWithValue("$rawMetricsTruncated", provider.RawMetricsTruncated ? 1 : 0);
            command.Parameters.AddWithValue("$rawMetricsBytes", provider.RawMetricsBytes);
            command.Parameters.AddWithValue("$rawMetrics", (object?)provider.RawMetrics ?? DBNull.Value);
            command.Parameters.AddWithValue("$snapshotJson", (object?)snapshotJson ?? DBNull.Value);
            command.Parameters.AddWithValue("$lastError", (object?)record.LastError ?? DBNull.Value);
        }

        private static void AddModelParameters(SqliteCommand command, ClientProviderTelemetrySampleRecord record, ClientProviderTelemetryModel model)
        {
            command.Parameters.AddWithValue("$id", IdGenerator.NewId("ptm_"));
            command.Parameters.AddWithValue("$sampleId", record.Id);
            command.Parameters.AddWithValue("$providerName", record.ProviderName);
            command.Parameters.AddWithValue("$modelName", model.Name);
            command.Parameters.AddWithValue("$family", (object?)model.Family ?? DBNull.Value);
            command.Parameters.AddWithValue("$quantization", (object?)model.Quantization ?? DBNull.Value);
            command.Parameters.AddWithValue("$parameterSize", (object?)model.ParameterSize ?? DBNull.Value);
            command.Parameters.AddWithValue("$contextLength", NullableInt(model.ContextLength));
            command.Parameters.AddWithValue("$loaded", model.Loaded ? 1 : 0);
            command.Parameters.AddWithValue("$sizeBytes", NullableLong(model.SizeBytes));
            command.Parameters.AddWithValue("$memoryBytes", NullableLong(model.MemoryBytes));
            command.Parameters.AddWithValue("$expiresUtc", model.ExpiresUtc.HasValue ? model.ExpiresUtc.Value.ToString("O", CultureInfo.InvariantCulture) : DBNull.Value);
        }

        private static void AddMetricParameters(SqliteCommand command, ClientProviderTelemetrySampleRecord record, ClientProviderTelemetryMetric metric)
        {
            command.Parameters.AddWithValue("$id", IdGenerator.NewId("ptm_"));
            command.Parameters.AddWithValue("$sampleId", record.Id);
            command.Parameters.AddWithValue("$providerName", record.ProviderName);
            command.Parameters.AddWithValue("$modelName", (object?)metric.ModelName ?? DBNull.Value);
            command.Parameters.AddWithValue("$metricName", metric.Name);
            command.Parameters.AddWithValue("$metricKind", metric.Kind);
            command.Parameters.AddWithValue("$metricValue", metric.Value);
            command.Parameters.AddWithValue("$metricUnit", (object?)metric.Unit ?? DBNull.Value);
            command.Parameters.AddWithValue("$source", metric.Source.ToString());
            command.Parameters.AddWithValue("$labelsJson", JsonUtil.Serialize(metric.Labels, false));
        }

        private static string SearchSql(string select, string metricColumn, ClientProviderTelemetrySearchRequest request, bool page)
        {
            string sql = "SELECT " + select + " FROM local_provider_telemetry_samples WHERE collected_utc >= $fromUtc AND collected_utc < $toUtc";
            if (!String.IsNullOrWhiteSpace(request.ProviderName)) sql += " AND provider_name = $providerName";
            if (request.Status.HasValue) sql += " AND status = $status";
            if (!String.IsNullOrWhiteSpace(request.ModelName)) sql += " AND id IN (SELECT sample_id FROM local_provider_telemetry_models WHERE model_name LIKE $modelName)";
            if (metricColumn.Length > 0) sql += " AND " + metricColumn + " IS NOT NULL";
            if (page) sql += " ORDER BY collected_utc DESC LIMIT $limit OFFSET $offset";
            sql += ";";
            return sql;
        }

        private static string RollupSql(string metricColumn, ClientProviderTelemetryRollupRequest request)
        {
            string sql = "SELECT collected_utc, " + metricColumn + " AS metric FROM local_provider_telemetry_samples WHERE collected_utc >= $fromUtc AND collected_utc < $toUtc AND " + metricColumn + " IS NOT NULL";
            if (!String.IsNullOrWhiteSpace(request.ProviderName)) sql += " AND provider_name = $providerName";
            if (!String.IsNullOrWhiteSpace(request.ModelName)) sql += " AND id IN (SELECT sample_id FROM local_provider_telemetry_models WHERE model_name LIKE $modelName)";
            sql += " ORDER BY collected_utc ASC;";
            return sql;
        }

        private static void AddSearchParameters(SqliteCommand command, DateTime fromUtc, DateTime toUtc, ClientProviderTelemetrySearchRequest request)
        {
            command.Parameters.AddWithValue("$fromUtc", fromUtc.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$toUtc", toUtc.ToString("O", CultureInfo.InvariantCulture));
            if (!String.IsNullOrWhiteSpace(request.ProviderName)) command.Parameters.AddWithValue("$providerName", request.ProviderName);
            if (request.Status.HasValue) command.Parameters.AddWithValue("$status", request.Status.Value.ToString());
            if (!String.IsNullOrWhiteSpace(request.ModelName)) command.Parameters.AddWithValue("$modelName", "%" + request.ModelName + "%");
        }

        private static void AddRollupParameters(SqliteCommand command, DateTime fromUtc, DateTime toUtc, ClientProviderTelemetryRollupRequest request)
        {
            command.Parameters.AddWithValue("$fromUtc", fromUtc.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$toUtc", toUtc.ToString("O", CultureInfo.InvariantCulture));
            if (!String.IsNullOrWhiteSpace(request.ProviderName)) command.Parameters.AddWithValue("$providerName", request.ProviderName);
            if (!String.IsNullOrWhiteSpace(request.ModelName)) command.Parameters.AddWithValue("$modelName", "%" + request.ModelName + "%");
        }

        private static string MetricColumn(ClientProviderTelemetryMetricEnum metric)
        {
            if (metric == ClientProviderTelemetryMetricEnum.QueuedRequests) return "queued_requests";
            if (metric == ClientProviderTelemetryMetricEnum.WaitingRequests) return "waiting_requests";
            if (metric == ClientProviderTelemetryMetricEnum.RequestRatePerSecond) return "request_rate_per_second";
            if (metric == ClientProviderTelemetryMetricEnum.ErrorRatePerSecond) return "error_rate_per_second";
            if (metric == ClientProviderTelemetryMetricEnum.PromptTokensPerSecond) return "prompt_tokens_per_second";
            if (metric == ClientProviderTelemetryMetricEnum.GenerationTokensPerSecond) return "generation_tokens_per_second";
            if (metric == ClientProviderTelemetryMetricEnum.TotalTokensPerSecond) return "total_tokens_per_second";
            if (metric == ClientProviderTelemetryMetricEnum.LatencyP95Ms) return "latency_p95_ms";
            if (metric == ClientProviderTelemetryMetricEnum.TimeToFirstTokenP95Ms) return "time_to_first_token_p95_ms";
            if (metric == ClientProviderTelemetryMetricEnum.CacheUsagePercent) return "cache_usage_percent";
            if (metric == ClientProviderTelemetryMetricEnum.LoadedModelCount) return "loaded_model_count";
            if (metric == ClientProviderTelemetryMetricEnum.AvailableModelCount) return "available_model_count";
            return "active_requests";
        }

        private static List<ClientProviderTelemetryRollupBucket> BuildEmptyBuckets(DateTime fromUtc, DateTime toUtc, int bucketMinutes)
        {
            List<ClientProviderTelemetryRollupBucket> buckets = new List<ClientProviderTelemetryRollupBucket>();
            DateTime cursor = fromUtc;
            while (cursor < toUtc)
            {
                DateTime end = cursor.AddMinutes(bucketMinutes);
                buckets.Add(new ClientProviderTelemetryRollupBucket { StartUtc = cursor, EndUtc = end, Label = cursor.ToString("HH:mm", CultureInfo.InvariantCulture) });
                cursor = end;
            }

            if (buckets.Count == 0) buckets.Add(new ClientProviderTelemetryRollupBucket { StartUtc = fromUtc, EndUtc = toUtc, Label = fromUtc.ToString("HH:mm", CultureInfo.InvariantCulture) });
            return buckets;
        }

        private static void ApplyBucketValue(ClientProviderTelemetryRollupBucket bucket, double value)
        {
            double previousTotal = (bucket.Average ?? 0) * bucket.Count;
            bucket.Count++;
            bucket.Average = Math.Round((previousTotal + value) / bucket.Count, 2);
            bucket.Minimum = bucket.Minimum.HasValue ? Math.Min(bucket.Minimum.Value, value) : value;
            bucket.Maximum = bucket.Maximum.HasValue ? Math.Max(bucket.Maximum.Value, value) : value;
        }

        private static async Task<List<ClientProviderTelemetryModelSampleRecord>> ReadModelsAsync(SqliteConnection connection, string sampleId, CancellationToken token)
        {
            List<ClientProviderTelemetryModelSampleRecord> models = new List<ClientProviderTelemetryModelSampleRecord>();
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT * FROM local_provider_telemetry_models WHERE sample_id = $sampleId ORDER BY model_name ASC;";
                command.Parameters.AddWithValue("$sampleId", sampleId);
                using (SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        models.Add(ReadModel(reader));
                    }
                }
            }

            return models;
        }

        private static async Task<List<ClientProviderTelemetryMetricSampleRecord>> ReadMetricsAsync(SqliteConnection connection, string sampleId, CancellationToken token)
        {
            List<ClientProviderTelemetryMetricSampleRecord> metrics = new List<ClientProviderTelemetryMetricSampleRecord>();
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT * FROM local_provider_telemetry_metrics WHERE sample_id = $sampleId ORDER BY metric_name ASC LIMIT 500;";
                command.Parameters.AddWithValue("$sampleId", sampleId);
                using (SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        metrics.Add(ReadMetric(reader));
                    }
                }
            }

            return metrics;
        }

        private static ClientProviderTelemetrySampleRecord ReadSample(SqliteDataReader reader)
        {
            return new ClientProviderTelemetrySampleRecord
            {
                Id = GetString(reader, "id"),
                ClientId = GetNullableString(reader, "client_id"),
                ProviderName = GetString(reader, "provider_name"),
                ApiType = GetString(reader, "api_type"),
                ProviderKind = Enum.TryParse(GetString(reader, "provider_kind"), out ClientProviderTelemetryKindEnum kind) ? kind : ClientProviderTelemetryKindEnum.Unknown,
                EndpointHash = GetString(reader, "endpoint_hash"),
                EndpointDisplay = GetString(reader, "endpoint_display"),
                CollectedUtc = DateTime.Parse(GetString(reader, "collected_utc"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                PersistedUtc = DateTime.Parse(GetString(reader, "persisted_utc"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                Status = Enum.TryParse(GetString(reader, "status"), out ClientProviderTelemetryStatusEnum status) ? status : ClientProviderTelemetryStatusEnum.Unavailable,
                HealthStatus = Enum.TryParse(GetString(reader, "health_status"), out ClientProviderTelemetryHealthStatusEnum health) ? health : ClientProviderTelemetryHealthStatusEnum.Unknown,
                Version = GetNullableString(reader, "version"),
                AvailableModelCount = GetInt32(reader, "available_model_count"),
                LoadedModelCount = GetInt32(reader, "loaded_model_count"),
                ActiveRequests = GetNullableDouble(reader, "active_requests"),
                QueuedRequests = GetNullableDouble(reader, "queued_requests"),
                WaitingRequests = GetNullableDouble(reader, "waiting_requests"),
                PromptTokensPerSecond = GetNullableDouble(reader, "prompt_tokens_per_second"),
                GenerationTokensPerSecond = GetNullableDouble(reader, "generation_tokens_per_second"),
                TotalTokensPerSecond = GetNullableDouble(reader, "total_tokens_per_second"),
                RequestRatePerSecond = GetNullableDouble(reader, "request_rate_per_second"),
                ErrorRatePerSecond = GetNullableDouble(reader, "error_rate_per_second"),
                LatencyP95Ms = GetNullableDouble(reader, "latency_p95_ms"),
                TimeToFirstTokenP95Ms = GetNullableDouble(reader, "time_to_first_token_p95_ms"),
                CacheUsagePercent = GetNullableDouble(reader, "cache_usage_percent"),
                ProcessId = GetNullableInt(reader, "process_id"),
                ProcessName = GetNullableString(reader, "process_name"),
                LastError = GetNullableString(reader, "last_error")
            };
        }

        private static ClientProviderTelemetryModelSampleRecord ReadModel(SqliteDataReader reader)
        {
            return new ClientProviderTelemetryModelSampleRecord
            {
                Id = GetString(reader, "id"),
                SampleId = GetString(reader, "sample_id"),
                ProviderName = GetString(reader, "provider_name"),
                ModelName = GetString(reader, "model_name"),
                Family = GetNullableString(reader, "family"),
                Quantization = GetNullableString(reader, "quantization"),
                ParameterSize = GetNullableString(reader, "parameter_size"),
                ContextLength = GetNullableInt(reader, "context_length"),
                Loaded = GetInt32(reader, "loaded") == 1,
                SizeBytes = GetNullableLong(reader, "size_bytes"),
                MemoryBytes = GetNullableLong(reader, "memory_bytes"),
                ExpiresUtc = GetNullableDateTime(reader, "expires_utc")
            };
        }

        private static ClientProviderTelemetryMetricSampleRecord ReadMetric(SqliteDataReader reader)
        {
            ClientProviderTelemetryMetricSampleRecord record = new ClientProviderTelemetryMetricSampleRecord
            {
                Id = GetString(reader, "id"),
                SampleId = GetString(reader, "sample_id"),
                ProviderName = GetString(reader, "provider_name"),
                ModelName = GetNullableString(reader, "model_name"),
                MetricName = GetString(reader, "metric_name"),
                MetricKind = GetString(reader, "metric_kind"),
                MetricValue = GetDouble(reader, "metric_value"),
                MetricUnit = GetNullableString(reader, "metric_unit"),
                Source = Enum.TryParse(GetString(reader, "source"), out ClientProviderTelemetryMetricSourceEnum source) ? source : ClientProviderTelemetryMetricSourceEnum.Unknown
            };
            string? labelsJson = GetNullableString(reader, "labels_json");
            if (!String.IsNullOrWhiteSpace(labelsJson))
            {
                List<ClientProviderTelemetryLabel>? labels = JsonUtil.Deserialize<List<ClientProviderTelemetryLabel>>(labelsJson);
                record.Labels = labels ?? new List<ClientProviderTelemetryLabel>();
            }

            return record;
        }

        private static object NullableDouble(double? value)
        {
            return value.HasValue ? value.Value : DBNull.Value;
        }

        private static object NullableLong(long? value)
        {
            return value.HasValue ? value.Value : DBNull.Value;
        }

        private static object NullableInt(int? value)
        {
            return value.HasValue ? value.Value : DBNull.Value;
        }

        private static string GetString(SqliteDataReader reader, string name)
        {
            object value = reader[name];
            return value == DBNull.Value ? string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private static string? GetNullableString(SqliteDataReader reader, string name)
        {
            object value = reader[name];
            return value == DBNull.Value ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static int GetInt32(SqliteDataReader reader, string name)
        {
            object value = reader[name];
            return value == DBNull.Value ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        private static int? GetNullableInt(SqliteDataReader reader, string name)
        {
            object value = reader[name];
            return value == DBNull.Value ? null : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        private static double GetDouble(SqliteDataReader reader, string name)
        {
            object value = reader[name];
            return value == DBNull.Value ? 0 : Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }

        private static double? GetNullableDouble(SqliteDataReader reader, string name)
        {
            object value = reader[name];
            return value == DBNull.Value ? null : Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }

        private static long? GetNullableLong(SqliteDataReader reader, string name)
        {
            object value = reader[name];
            return value == DBNull.Value ? null : Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }

        private static DateTime? GetNullableDateTime(SqliteDataReader reader, string name)
        {
            string? value = GetNullableString(reader, name);
            return String.IsNullOrWhiteSpace(value) ? null : DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }

        private static string InsertSampleSql()
        {
            return @"
INSERT INTO local_provider_telemetry_samples
(
    id, client_id, provider_name, api_type, provider_kind, endpoint_hash, endpoint_display,
    collected_utc, persisted_utc, status, health_status, version, available_model_count,
    loaded_model_count, active_requests, queued_requests, waiting_requests, swapped_requests,
    prompt_tokens_per_second, generation_tokens_per_second, total_tokens_per_second,
    request_rate_per_second, error_rate_per_second, latency_p50_ms, latency_p95_ms,
    latency_p99_ms, time_to_first_token_p50_ms, time_to_first_token_p95_ms,
    cache_usage_percent, gpu_cache_usage_percent, cpu_cache_usage_percent, memory_used_bytes,
    memory_total_bytes, process_id, process_name, raw_metrics_truncated, raw_metrics_bytes, raw_metrics, snapshot_json, last_error
)
VALUES
(
    $id, $clientId, $providerName, $apiType, $providerKind, $endpointHash, $endpointDisplay,
    $collectedUtc, $persistedUtc, $status, $healthStatus, $version, $availableModelCount,
    $loadedModelCount, $activeRequests, $queuedRequests, $waitingRequests, $swappedRequests,
    $promptTokensPerSecond, $generationTokensPerSecond, $totalTokensPerSecond,
    $requestRatePerSecond, $errorRatePerSecond, $latencyP50Ms, $latencyP95Ms,
    $latencyP99Ms, $timeToFirstTokenP50Ms, $timeToFirstTokenP95Ms,
    $cacheUsagePercent, $gpuCacheUsagePercent, $cpuCacheUsagePercent, $memoryUsedBytes,
    $memoryTotalBytes, $processId, $processName, $rawMetricsTruncated, $rawMetricsBytes, $rawMetrics, $snapshotJson, $lastError
);";
        }

        private static string InsertModelSql()
        {
            return @"
INSERT INTO local_provider_telemetry_models
(
    id, sample_id, provider_name, model_name, family, quantization, parameter_size,
    context_length, loaded, size_bytes, memory_bytes, expires_utc
)
VALUES
(
    $id, $sampleId, $providerName, $modelName, $family, $quantization, $parameterSize,
    $contextLength, $loaded, $sizeBytes, $memoryBytes, $expiresUtc
);";
        }

        private static string InsertMetricSql()
        {
            return @"
INSERT INTO local_provider_telemetry_metrics
(
    id, sample_id, provider_name, model_name, metric_name, metric_kind, metric_value,
    metric_unit, source, labels_json
)
VALUES
(
    $id, $sampleId, $providerName, $modelName, $metricName, $metricKind, $metricValue,
    $metricUnit, $source, $labelsJson
);";
        }

        private static string SchemaSql()
        {
            return @"
CREATE TABLE IF NOT EXISTS local_provider_telemetry_samples
(
    id TEXT PRIMARY KEY,
    client_id TEXT NULL,
    provider_name TEXT NOT NULL,
    api_type TEXT NOT NULL,
    provider_kind TEXT NOT NULL,
    endpoint_hash TEXT NOT NULL,
    endpoint_display TEXT NOT NULL,
    collected_utc TEXT NOT NULL,
    persisted_utc TEXT NOT NULL,
    status TEXT NOT NULL,
    health_status TEXT NOT NULL,
    version TEXT NULL,
    available_model_count INTEGER NOT NULL,
    loaded_model_count INTEGER NOT NULL,
    active_requests REAL NULL,
    queued_requests REAL NULL,
    waiting_requests REAL NULL,
    swapped_requests REAL NULL,
    prompt_tokens_per_second REAL NULL,
    generation_tokens_per_second REAL NULL,
    total_tokens_per_second REAL NULL,
    request_rate_per_second REAL NULL,
    error_rate_per_second REAL NULL,
    latency_p50_ms REAL NULL,
    latency_p95_ms REAL NULL,
    latency_p99_ms REAL NULL,
    time_to_first_token_p50_ms REAL NULL,
    time_to_first_token_p95_ms REAL NULL,
    cache_usage_percent REAL NULL,
    gpu_cache_usage_percent REAL NULL,
    cpu_cache_usage_percent REAL NULL,
    memory_used_bytes INTEGER NULL,
    memory_total_bytes INTEGER NULL,
    process_id INTEGER NULL,
    process_name TEXT NULL,
    raw_metrics_truncated INTEGER NOT NULL,
    raw_metrics_bytes INTEGER NOT NULL,
    raw_metrics TEXT NULL,
    snapshot_json TEXT NULL,
    last_error TEXT NULL
);
CREATE TABLE IF NOT EXISTS local_provider_telemetry_models
(
    id TEXT PRIMARY KEY,
    sample_id TEXT NOT NULL,
    provider_name TEXT NOT NULL,
    model_name TEXT NOT NULL,
    family TEXT NULL,
    quantization TEXT NULL,
    parameter_size TEXT NULL,
    context_length INTEGER NULL,
    loaded INTEGER NOT NULL,
    size_bytes INTEGER NULL,
    memory_bytes INTEGER NULL,
    expires_utc TEXT NULL,
    FOREIGN KEY(sample_id) REFERENCES local_provider_telemetry_samples(id) ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS local_provider_telemetry_metrics
(
    id TEXT PRIMARY KEY,
    sample_id TEXT NOT NULL,
    provider_name TEXT NOT NULL,
    model_name TEXT NULL,
    metric_name TEXT NOT NULL,
    metric_kind TEXT NOT NULL,
    metric_value REAL NOT NULL,
    metric_unit TEXT NULL,
    source TEXT NOT NULL,
    labels_json TEXT NULL,
    FOREIGN KEY(sample_id) REFERENCES local_provider_telemetry_samples(id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS idx_local_provider_telemetry_provider_collected ON local_provider_telemetry_samples(provider_name, collected_utc);
CREATE INDEX IF NOT EXISTS idx_local_provider_telemetry_kind_collected ON local_provider_telemetry_samples(provider_kind, collected_utc);
CREATE INDEX IF NOT EXISTS idx_local_provider_telemetry_status_collected ON local_provider_telemetry_samples(status, collected_utc);
CREATE INDEX IF NOT EXISTS idx_local_provider_telemetry_endpoint_collected ON local_provider_telemetry_samples(endpoint_hash, collected_utc);
CREATE INDEX IF NOT EXISTS idx_local_provider_telemetry_models_sample ON local_provider_telemetry_models(sample_id);
CREATE INDEX IF NOT EXISTS idx_local_provider_telemetry_models_model ON local_provider_telemetry_models(model_name, provider_name);
CREATE INDEX IF NOT EXISTS idx_local_provider_telemetry_metrics_sample ON local_provider_telemetry_metrics(sample_id);
CREATE INDEX IF NOT EXISTS idx_local_provider_telemetry_metrics_metric ON local_provider_telemetry_metrics(metric_name, provider_name);
";
        }
    }
}

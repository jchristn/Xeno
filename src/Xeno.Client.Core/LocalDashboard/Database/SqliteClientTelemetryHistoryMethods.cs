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
    /// SQLite client telemetry history methods.
    /// </summary>
    public sealed class SqliteClientTelemetryHistoryMethods : IClientTelemetryHistoryMethods
    {
        private readonly string _ConnectionString;

        /// <summary>
        /// Instantiate SQLite telemetry history methods.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        public SqliteClientTelemetryHistoryMethods(ClientLocalDatabaseSettings settings)
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
            }
        }

        /// <inheritdoc />
        public async Task<ClientTelemetrySampleRecord> CreateAsync(ClientTelemetrySnapshot snapshot, ClientTelemetrySettings settings, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            ArgumentNullException.ThrowIfNull(settings);
            token.ThrowIfCancellationRequested();
            ClientTelemetrySampleRecord record = BuildRecord(snapshot);
            string? snapshotJson = settings.PersistRawSnapshot ? JsonUtil.Serialize(snapshot, true) : null;
            using (SqliteConnection connection = new SqliteConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqliteTransaction transaction = connection.BeginTransaction())
                {
                    using (SqliteCommand command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = InsertSampleSql();
                        AddSampleParameters(command, record, snapshotJson);
                        await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }

                    foreach (ClientGpuDeviceTelemetry device in snapshot.Gpu.Devices)
                    {
                        using (SqliteCommand command = connection.CreateCommand())
                        {
                            command.Transaction = transaction;
                            command.CommandText = InsertGpuSql();
                            AddGpuParameters(command, record.Id, device);
                            await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                        }
                    }

                    transaction.Commit();
                }
            }

            return record;
        }

        /// <inheritdoc />
        public async Task<ClientTelemetrySampleDetail?> ReadLatestAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            using (SqliteConnection connection = new SqliteConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM local_telemetry_samples ORDER BY collected_utc DESC LIMIT 1;";
                    using (SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        if (!await reader.ReadAsync(token).ConfigureAwait(false)) return null;
                        ClientTelemetrySampleDetail detail = new ClientTelemetrySampleDetail
                        {
                            Sample = ReadSample(reader),
                            SnapshotJson = GetNullableString(reader, "snapshot_json")
                        };
                        reader.Close();
                        detail.Gpus = await ReadGpusAsync(connection, detail.Sample.Id, token).ConfigureAwait(false);
                        return detail;
                    }
                }
            }
        }

        /// <inheritdoc />
        public async Task<ClientTelemetrySampleDetail?> ReadDetailAsync(string id, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            using (SqliteConnection connection = new SqliteConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM local_telemetry_samples WHERE id = $id LIMIT 1;";
                    command.Parameters.AddWithValue("$id", id);
                    using (SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        if (!await reader.ReadAsync(token).ConfigureAwait(false)) return null;
                        ClientTelemetrySampleDetail detail = new ClientTelemetrySampleDetail
                        {
                            Sample = ReadSample(reader),
                            SnapshotJson = GetNullableString(reader, "snapshot_json")
                        };
                        reader.Close();
                        detail.Gpus = await ReadGpusAsync(connection, detail.Sample.Id, token).ConfigureAwait(false);
                        return detail;
                    }
                }
            }
        }

        /// <inheritdoc />
        public async Task<ClientTelemetrySearchResult> SearchAsync(ClientTelemetrySearchRequest request, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(request);
            token.ThrowIfCancellationRequested();
            DateTime toUtc = request.ToUtc ?? DateTime.UtcNow;
            DateTime fromUtc = request.FromUtc ?? toUtc.AddHours(-1);
            string metricColumn = MetricColumn(request.Metric);
            ClientTelemetrySearchResult result = new ClientTelemetrySearchResult();
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
                    command.Parameters.AddWithValue("$limit", Math.Clamp(request.Limit, 1, 1000));
                    command.Parameters.AddWithValue("$offset", Math.Max(0, request.Offset));
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
        public async Task<IReadOnlyList<ClientTelemetryRollupBucket>> RollupAsync(ClientTelemetryRollupRequest request, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(request);
            token.ThrowIfCancellationRequested();
            DateTime toUtc = request.ToUtc ?? DateTime.UtcNow;
            DateTime fromUtc = request.FromUtc ?? toUtc.AddHours(-1);
            int bucketMinutes = Math.Clamp(request.BucketMinutes, 1, 1440);
            string metricColumn = MetricColumn(request.Metric);
            List<ClientTelemetryRollupBucket> buckets = BuildEmptyBuckets(fromUtc, toUtc, bucketMinutes);
            using (SqliteConnection connection = new SqliteConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT collected_utc, " + metricColumn + " AS metric FROM local_telemetry_samples WHERE collected_utc >= $fromUtc AND collected_utc < $toUtc AND " + metricColumn + " IS NOT NULL ORDER BY collected_utc ASC;";
                    command.Parameters.AddWithValue("$fromUtc", fromUtc.ToString("O", CultureInfo.InvariantCulture));
                    command.Parameters.AddWithValue("$toUtc", toUtc.ToString("O", CultureInfo.InvariantCulture));
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
                    command.CommandText = "DELETE FROM local_telemetry_samples WHERE id = $id;";
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
                    command.CommandText = "DELETE FROM local_telemetry_samples;";
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
                    command.CommandText = "DELETE FROM local_telemetry_samples WHERE collected_utc < $olderThanUtc;";
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
                Enabled = settings.Enabled,
                CollectionIntervalMs = settings.CollectionIntervalMs,
                RetentionDays = settings.RetentionDays
            };
            using (SqliteConnection connection = new SqliteConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT COUNT(*) AS samples, MIN(collected_utc) AS oldest, MAX(collected_utc) AS newest FROM local_telemetry_samples;";
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

        private static ClientTelemetrySampleRecord BuildRecord(ClientTelemetrySnapshot snapshot)
        {
            return new ClientTelemetrySampleRecord
            {
                Id = snapshot.Metadata.Id,
                ClientId = snapshot.Metadata.ClientId,
                MachineName = snapshot.Metadata.MachineName,
                CollectedUtc = snapshot.Metadata.CollectedUtc,
                PersistedUtc = DateTime.UtcNow,
                CollectionElapsedMs = snapshot.Metadata.CollectionElapsedMs,
                OsDescription = snapshot.System.OsDescription,
                Architecture = snapshot.System.Architecture,
                ProcessorCount = snapshot.System.ProcessorCount,
                CpuUtilizationPercent = snapshot.Cpu.UtilizationPercent,
                MemoryTotalBytes = snapshot.Memory.TotalBytes,
                MemoryUsedBytes = snapshot.Memory.UsedBytes,
                MemoryAvailableBytes = snapshot.Memory.AvailableBytes,
                MemoryUtilizationPercent = snapshot.Memory.UtilizationPercent,
                DiskTotalBytes = snapshot.Disk.TotalBytes,
                DiskUsedBytes = snapshot.Disk.UsedBytes,
                DiskAvailableBytes = snapshot.Disk.AvailableBytes,
                DiskUtilizationPercent = snapshot.Disk.UtilizationPercent,
                DiskReadBytesPerSecond = snapshot.Disk.ReadBytesPerSecond,
                DiskWriteBytesPerSecond = snapshot.Disk.WriteBytesPerSecond,
                GpuProvider = snapshot.Gpu.Provider,
                GpuDeviceCount = snapshot.Gpu.DeviceCount,
                GpuAverageUtilizationPercent = snapshot.Gpu.AverageUtilizationPercent,
                GpuAverageMemoryUtilizationPercent = snapshot.Gpu.AverageMemoryUtilizationPercent,
                GpuAverageTemperatureCelsius = snapshot.Gpu.AverageTemperatureCelsius,
                GpuAveragePowerWatts = snapshot.Gpu.AveragePowerWatts,
                GpuTotalMemoryBytes = snapshot.Gpu.TotalMemoryBytes,
                GpuUsedMemoryBytes = snapshot.Gpu.UsedMemoryBytes
            };
        }

        private static void AddSampleParameters(SqliteCommand command, ClientTelemetrySampleRecord record, string? snapshotJson)
        {
            command.Parameters.AddWithValue("$id", record.Id);
            command.Parameters.AddWithValue("$clientId", (object?)record.ClientId ?? DBNull.Value);
            command.Parameters.AddWithValue("$machineName", record.MachineName);
            command.Parameters.AddWithValue("$collectedUtc", record.CollectedUtc.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$persistedUtc", record.PersistedUtc.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$collectionElapsedMs", record.CollectionElapsedMs);
            command.Parameters.AddWithValue("$osDescription", record.OsDescription);
            command.Parameters.AddWithValue("$architecture", record.Architecture);
            command.Parameters.AddWithValue("$processorCount", record.ProcessorCount);
            command.Parameters.AddWithValue("$cpuUtilizationPercent", NullableDouble(record.CpuUtilizationPercent));
            command.Parameters.AddWithValue("$memoryTotalBytes", NullableLong(record.MemoryTotalBytes));
            command.Parameters.AddWithValue("$memoryUsedBytes", NullableLong(record.MemoryUsedBytes));
            command.Parameters.AddWithValue("$memoryAvailableBytes", NullableLong(record.MemoryAvailableBytes));
            command.Parameters.AddWithValue("$memoryUtilizationPercent", NullableDouble(record.MemoryUtilizationPercent));
            command.Parameters.AddWithValue("$diskTotalBytes", NullableLong(record.DiskTotalBytes));
            command.Parameters.AddWithValue("$diskUsedBytes", NullableLong(record.DiskUsedBytes));
            command.Parameters.AddWithValue("$diskAvailableBytes", NullableLong(record.DiskAvailableBytes));
            command.Parameters.AddWithValue("$diskUtilizationPercent", NullableDouble(record.DiskUtilizationPercent));
            command.Parameters.AddWithValue("$diskReadBytesPerSecond", NullableDouble(record.DiskReadBytesPerSecond));
            command.Parameters.AddWithValue("$diskWriteBytesPerSecond", NullableDouble(record.DiskWriteBytesPerSecond));
            command.Parameters.AddWithValue("$gpuProvider", record.GpuProvider.ToString());
            command.Parameters.AddWithValue("$gpuDeviceCount", record.GpuDeviceCount);
            command.Parameters.AddWithValue("$gpuAverageUtilizationPercent", NullableDouble(record.GpuAverageUtilizationPercent));
            command.Parameters.AddWithValue("$gpuAverageMemoryUtilizationPercent", NullableDouble(record.GpuAverageMemoryUtilizationPercent));
            command.Parameters.AddWithValue("$gpuAverageTemperatureCelsius", NullableDouble(record.GpuAverageTemperatureCelsius));
            command.Parameters.AddWithValue("$gpuAveragePowerWatts", NullableDouble(record.GpuAveragePowerWatts));
            command.Parameters.AddWithValue("$gpuTotalMemoryBytes", NullableLong(record.GpuTotalMemoryBytes));
            command.Parameters.AddWithValue("$gpuUsedMemoryBytes", NullableLong(record.GpuUsedMemoryBytes));
            command.Parameters.AddWithValue("$snapshotJson", (object?)snapshotJson ?? DBNull.Value);
        }

        private static void AddGpuParameters(SqliteCommand command, string sampleId, ClientGpuDeviceTelemetry device)
        {
            command.Parameters.AddWithValue("$id", IdGenerator.NewId("tgd_"));
            command.Parameters.AddWithValue("$sampleId", sampleId);
            command.Parameters.AddWithValue("$gpuUuid", device.Uuid);
            command.Parameters.AddWithValue("$name", device.Name);
            command.Parameters.AddWithValue("$busId", (object?)device.BusId ?? DBNull.Value);
            command.Parameters.AddWithValue("$driverVersion", (object?)device.DriverVersion ?? DBNull.Value);
            command.Parameters.AddWithValue("$utilizationPercent", NullableDouble(device.UtilizationPercent));
            command.Parameters.AddWithValue("$memoryTotalBytes", NullableLong(device.MemoryTotalBytes));
            command.Parameters.AddWithValue("$memoryUsedBytes", NullableLong(device.MemoryUsedBytes));
            command.Parameters.AddWithValue("$memoryFreeBytes", NullableLong(device.MemoryFreeBytes));
            command.Parameters.AddWithValue("$memoryUtilizationPercent", NullableDouble(device.MemoryUtilizationPercent));
            command.Parameters.AddWithValue("$temperatureCelsius", NullableDouble(device.TemperatureCelsius));
            command.Parameters.AddWithValue("$powerWatts", NullableDouble(device.PowerWatts));
        }

        private static object NullableDouble(double? value)
        {
            return value.HasValue ? value.Value : DBNull.Value;
        }

        private static object NullableLong(long? value)
        {
            return value.HasValue ? value.Value : DBNull.Value;
        }

        private static string SearchSql(string select, string metricColumn, ClientTelemetrySearchRequest request, bool page)
        {
            string sql = "SELECT " + select + " FROM local_telemetry_samples WHERE collected_utc >= $fromUtc AND collected_utc < $toUtc";
            sql += " AND (" + metricColumn + " IS NOT NULL)";
            if (request.MinValue.HasValue) sql += " AND " + metricColumn + " >= $minValue";
            if (request.MaxValue.HasValue) sql += " AND " + metricColumn + " <= $maxValue";
            if (!String.IsNullOrWhiteSpace(request.Platform)) sql += " AND os_description LIKE $platform";
            if (!String.IsNullOrWhiteSpace(request.GpuUuid) || !String.IsNullOrWhiteSpace(request.GpuModel))
            {
                sql += " AND id IN (SELECT sample_id FROM local_telemetry_gpu_samples WHERE 1 = 1";
                if (!String.IsNullOrWhiteSpace(request.GpuUuid)) sql += " AND gpu_uuid LIKE $gpuUuid";
                if (!String.IsNullOrWhiteSpace(request.GpuModel)) sql += " AND name LIKE $gpuModel";
                sql += ")";
            }

            if (page) sql += " ORDER BY collected_utc DESC LIMIT $limit OFFSET $offset";
            sql += ";";
            return sql;
        }

        private static void AddSearchParameters(SqliteCommand command, DateTime fromUtc, DateTime toUtc, ClientTelemetrySearchRequest request)
        {
            command.Parameters.AddWithValue("$fromUtc", fromUtc.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$toUtc", toUtc.ToString("O", CultureInfo.InvariantCulture));
            if (request.MinValue.HasValue) command.Parameters.AddWithValue("$minValue", request.MinValue.Value);
            if (request.MaxValue.HasValue) command.Parameters.AddWithValue("$maxValue", request.MaxValue.Value);
            if (!String.IsNullOrWhiteSpace(request.Platform)) command.Parameters.AddWithValue("$platform", "%" + request.Platform + "%");
            if (!String.IsNullOrWhiteSpace(request.GpuUuid)) command.Parameters.AddWithValue("$gpuUuid", "%" + request.GpuUuid + "%");
            if (!String.IsNullOrWhiteSpace(request.GpuModel)) command.Parameters.AddWithValue("$gpuModel", "%" + request.GpuModel + "%");
        }

        private static string MetricColumn(ClientTelemetryMetricEnum metric)
        {
            if (metric == ClientTelemetryMetricEnum.MemoryUtilizationPercent) return "memory_utilization_percent";
            if (metric == ClientTelemetryMetricEnum.DiskUtilizationPercent) return "disk_utilization_percent";
            if (metric == ClientTelemetryMetricEnum.GpuUtilizationPercent) return "gpu_average_utilization_percent";
            if (metric == ClientTelemetryMetricEnum.GpuMemoryUtilizationPercent) return "gpu_average_memory_utilization_percent";
            if (metric == ClientTelemetryMetricEnum.GpuTemperatureCelsius) return "gpu_average_temperature_celsius";
            if (metric == ClientTelemetryMetricEnum.GpuPowerWatts) return "gpu_average_power_watts";
            return "cpu_utilization_percent";
        }

        private static List<ClientTelemetryRollupBucket> BuildEmptyBuckets(DateTime fromUtc, DateTime toUtc, int bucketMinutes)
        {
            List<ClientTelemetryRollupBucket> buckets = new List<ClientTelemetryRollupBucket>();
            DateTime cursor = fromUtc;
            while (cursor < toUtc)
            {
                DateTime end = cursor.AddMinutes(bucketMinutes);
                buckets.Add(new ClientTelemetryRollupBucket
                {
                    StartUtc = cursor,
                    EndUtc = end,
                    Label = cursor.ToString("HH:mm", CultureInfo.InvariantCulture)
                });
                cursor = end;
            }

            if (buckets.Count == 0) buckets.Add(new ClientTelemetryRollupBucket { StartUtc = fromUtc, EndUtc = toUtc, Label = fromUtc.ToString("HH:mm", CultureInfo.InvariantCulture) });
            return buckets;
        }

        private static void ApplyBucketValue(ClientTelemetryRollupBucket bucket, double value)
        {
            double previousTotal = (bucket.Average ?? 0) * bucket.Count;
            bucket.Count++;
            bucket.Average = Math.Round((previousTotal + value) / bucket.Count, 2);
            bucket.Minimum = bucket.Minimum.HasValue ? Math.Min(bucket.Minimum.Value, value) : value;
            bucket.Maximum = bucket.Maximum.HasValue ? Math.Max(bucket.Maximum.Value, value) : value;
        }

        private static async Task<List<ClientTelemetryGpuSampleRecord>> ReadGpusAsync(SqliteConnection connection, string sampleId, CancellationToken token)
        {
            List<ClientTelemetryGpuSampleRecord> gpus = new List<ClientTelemetryGpuSampleRecord>();
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT * FROM local_telemetry_gpu_samples WHERE sample_id = $sampleId ORDER BY name ASC;";
                command.Parameters.AddWithValue("$sampleId", sampleId);
                using (SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        gpus.Add(new ClientTelemetryGpuSampleRecord
                        {
                            Id = GetString(reader, "id"),
                            SampleId = GetString(reader, "sample_id"),
                            Uuid = GetString(reader, "gpu_uuid"),
                            Name = GetString(reader, "name"),
                            BusId = GetNullableString(reader, "bus_id"),
                            DriverVersion = GetNullableString(reader, "driver_version"),
                            UtilizationPercent = GetNullableDouble(reader, "utilization_percent"),
                            MemoryTotalBytes = GetNullableLong(reader, "memory_total_bytes"),
                            MemoryUsedBytes = GetNullableLong(reader, "memory_used_bytes"),
                            MemoryFreeBytes = GetNullableLong(reader, "memory_free_bytes"),
                            MemoryUtilizationPercent = GetNullableDouble(reader, "memory_utilization_percent"),
                            TemperatureCelsius = GetNullableDouble(reader, "temperature_celsius"),
                            PowerWatts = GetNullableDouble(reader, "power_watts")
                        });
                    }
                }
            }

            return gpus;
        }

        private static ClientTelemetrySampleRecord ReadSample(SqliteDataReader reader)
        {
            return new ClientTelemetrySampleRecord
            {
                Id = GetString(reader, "id"),
                ClientId = GetNullableString(reader, "client_id"),
                MachineName = GetString(reader, "machine_name"),
                CollectedUtc = DateTime.Parse(GetString(reader, "collected_utc"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                PersistedUtc = DateTime.Parse(GetString(reader, "persisted_utc"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                CollectionElapsedMs = GetDouble(reader, "collection_elapsed_ms"),
                OsDescription = GetString(reader, "os_description"),
                Architecture = GetString(reader, "architecture"),
                ProcessorCount = GetInt32(reader, "processor_count"),
                CpuUtilizationPercent = GetNullableDouble(reader, "cpu_utilization_percent"),
                MemoryTotalBytes = GetNullableLong(reader, "memory_total_bytes"),
                MemoryUsedBytes = GetNullableLong(reader, "memory_used_bytes"),
                MemoryAvailableBytes = GetNullableLong(reader, "memory_available_bytes"),
                MemoryUtilizationPercent = GetNullableDouble(reader, "memory_utilization_percent"),
                DiskTotalBytes = GetNullableLong(reader, "disk_total_bytes"),
                DiskUsedBytes = GetNullableLong(reader, "disk_used_bytes"),
                DiskAvailableBytes = GetNullableLong(reader, "disk_available_bytes"),
                DiskUtilizationPercent = GetNullableDouble(reader, "disk_utilization_percent"),
                DiskReadBytesPerSecond = GetNullableDouble(reader, "disk_read_bytes_per_second"),
                DiskWriteBytesPerSecond = GetNullableDouble(reader, "disk_write_bytes_per_second"),
                GpuProvider = Enum.TryParse(GetString(reader, "gpu_provider"), out ClientTelemetryGpuProviderEnum provider) ? provider : ClientTelemetryGpuProviderEnum.None,
                GpuDeviceCount = GetInt32(reader, "gpu_device_count"),
                GpuAverageUtilizationPercent = GetNullableDouble(reader, "gpu_average_utilization_percent"),
                GpuAverageMemoryUtilizationPercent = GetNullableDouble(reader, "gpu_average_memory_utilization_percent"),
                GpuAverageTemperatureCelsius = GetNullableDouble(reader, "gpu_average_temperature_celsius"),
                GpuAveragePowerWatts = GetNullableDouble(reader, "gpu_average_power_watts"),
                GpuTotalMemoryBytes = GetNullableLong(reader, "gpu_total_memory_bytes"),
                GpuUsedMemoryBytes = GetNullableLong(reader, "gpu_used_memory_bytes")
            };
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
INSERT INTO local_telemetry_samples
(
    id, client_id, machine_name, collected_utc, persisted_utc, collection_elapsed_ms,
    os_description, architecture, processor_count, cpu_utilization_percent,
    memory_total_bytes, memory_used_bytes, memory_available_bytes, memory_utilization_percent,
    disk_total_bytes, disk_used_bytes, disk_available_bytes, disk_utilization_percent,
    disk_read_bytes_per_second, disk_write_bytes_per_second, gpu_provider, gpu_device_count,
    gpu_average_utilization_percent, gpu_average_memory_utilization_percent,
    gpu_average_temperature_celsius, gpu_average_power_watts, gpu_total_memory_bytes,
    gpu_used_memory_bytes, snapshot_json
)
VALUES
(
    $id, $clientId, $machineName, $collectedUtc, $persistedUtc, $collectionElapsedMs,
    $osDescription, $architecture, $processorCount, $cpuUtilizationPercent,
    $memoryTotalBytes, $memoryUsedBytes, $memoryAvailableBytes, $memoryUtilizationPercent,
    $diskTotalBytes, $diskUsedBytes, $diskAvailableBytes, $diskUtilizationPercent,
    $diskReadBytesPerSecond, $diskWriteBytesPerSecond, $gpuProvider, $gpuDeviceCount,
    $gpuAverageUtilizationPercent, $gpuAverageMemoryUtilizationPercent,
    $gpuAverageTemperatureCelsius, $gpuAveragePowerWatts, $gpuTotalMemoryBytes,
    $gpuUsedMemoryBytes, $snapshotJson
);";
        }

        private static string InsertGpuSql()
        {
            return @"
INSERT INTO local_telemetry_gpu_samples
(
    id, sample_id, gpu_uuid, name, bus_id, driver_version, utilization_percent,
    memory_total_bytes, memory_used_bytes, memory_free_bytes, memory_utilization_percent,
    temperature_celsius, power_watts
)
VALUES
(
    $id, $sampleId, $gpuUuid, $name, $busId, $driverVersion, $utilizationPercent,
    $memoryTotalBytes, $memoryUsedBytes, $memoryFreeBytes, $memoryUtilizationPercent,
    $temperatureCelsius, $powerWatts
);";
        }

        private static string SchemaSql()
        {
            return @"
CREATE TABLE IF NOT EXISTS local_telemetry_samples
(
    id TEXT PRIMARY KEY,
    client_id TEXT NULL,
    machine_name TEXT NOT NULL,
    collected_utc TEXT NOT NULL,
    persisted_utc TEXT NOT NULL,
    collection_elapsed_ms REAL NOT NULL,
    os_description TEXT NOT NULL,
    architecture TEXT NOT NULL,
    processor_count INTEGER NOT NULL,
    cpu_utilization_percent REAL NULL,
    memory_total_bytes INTEGER NULL,
    memory_used_bytes INTEGER NULL,
    memory_available_bytes INTEGER NULL,
    memory_utilization_percent REAL NULL,
    disk_total_bytes INTEGER NULL,
    disk_used_bytes INTEGER NULL,
    disk_available_bytes INTEGER NULL,
    disk_utilization_percent REAL NULL,
    disk_read_bytes_per_second REAL NULL,
    disk_write_bytes_per_second REAL NULL,
    gpu_provider TEXT NOT NULL,
    gpu_device_count INTEGER NOT NULL,
    gpu_average_utilization_percent REAL NULL,
    gpu_average_memory_utilization_percent REAL NULL,
    gpu_average_temperature_celsius REAL NULL,
    gpu_average_power_watts REAL NULL,
    gpu_total_memory_bytes INTEGER NULL,
    gpu_used_memory_bytes INTEGER NULL,
    snapshot_json TEXT NULL
);
CREATE TABLE IF NOT EXISTS local_telemetry_gpu_samples
(
    id TEXT PRIMARY KEY,
    sample_id TEXT NOT NULL,
    gpu_uuid TEXT NOT NULL,
    name TEXT NOT NULL,
    bus_id TEXT NULL,
    driver_version TEXT NULL,
    utilization_percent REAL NULL,
    memory_total_bytes INTEGER NULL,
    memory_used_bytes INTEGER NULL,
    memory_free_bytes INTEGER NULL,
    memory_utilization_percent REAL NULL,
    temperature_celsius REAL NULL,
    power_watts REAL NULL,
    FOREIGN KEY(sample_id) REFERENCES local_telemetry_samples(id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS idx_local_telemetry_samples_collected ON local_telemetry_samples(collected_utc);
CREATE INDEX IF NOT EXISTS idx_local_telemetry_samples_client_collected ON local_telemetry_samples(client_id, collected_utc);
CREATE INDEX IF NOT EXISTS idx_local_telemetry_samples_cpu_collected ON local_telemetry_samples(cpu_utilization_percent, collected_utc);
CREATE INDEX IF NOT EXISTS idx_local_telemetry_samples_memory_collected ON local_telemetry_samples(memory_utilization_percent, collected_utc);
CREATE INDEX IF NOT EXISTS idx_local_telemetry_samples_disk_collected ON local_telemetry_samples(disk_utilization_percent, collected_utc);
CREATE INDEX IF NOT EXISTS idx_local_telemetry_samples_gpu_collected ON local_telemetry_samples(gpu_average_utilization_percent, collected_utc);
CREATE INDEX IF NOT EXISTS idx_local_telemetry_gpu_sample ON local_telemetry_gpu_samples(sample_id);
CREATE INDEX IF NOT EXISTS idx_local_telemetry_gpu_uuid ON local_telemetry_gpu_samples(gpu_uuid);
";
        }
    }
}

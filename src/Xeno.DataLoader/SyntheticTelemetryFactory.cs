namespace Xeno.DataLoader
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading;
    using Xeno.Core.Serialization;

    internal sealed class SyntheticTelemetryFactory
    {
        private readonly DataLoaderOptions _Options;
        private readonly RandomSource _Random;
        private readonly List<SyntheticClientProfile> _Clients;

        public SyntheticTelemetryFactory(DataLoaderOptions options, RandomSource random, List<SyntheticClientProfile> clients)
        {
            _Options = options ?? throw new ArgumentNullException(nameof(options));
            _Random = random ?? throw new ArgumentNullException(nameof(random));
            _Clients = clients ?? throw new ArgumentNullException(nameof(clients));
        }

        public SyntheticTelemetryBatch CreateTelemetry(CancellationToken token)
        {
            SyntheticTelemetryBatch batch = new SyntheticTelemetryBatch();
            if (_Options.TelemetrySamplesPerHour == 0) return batch;

            foreach (SyntheticClientProfile client in _Clients)
            {
                token.ThrowIfCancellationRequested();
                CreateTelemetryForClient(client, batch, token);
            }

            batch.HostSamples.Sort((left, right) => left.CollectedUtc.CompareTo(right.CollectedUtc));
            batch.ProviderSamples.Sort((left, right) => left.CollectedUtc.CompareTo(right.CollectedUtc));
            batch.Attempts.Sort((left, right) => left.StartedUtc.CompareTo(right.StartedUtc));
            return batch;
        }

        private void CreateTelemetryForClient(SyntheticClientProfile client, SyntheticTelemetryBatch batch, CancellationToken token)
        {
            double minutesPerSample = 60.0 / _Options.TelemetrySamplesPerHour;
            DateTime cursor = _Options.FromUtc;
            int sampleIndex = 0;
            while (cursor < _Options.ToUtc)
            {
                token.ThrowIfCancellationRequested();
                if (!ShouldSkipSample(cursor))
                {
                    DateTime collectedUtc = cursor.AddSeconds(_Random.NextInt32(0, Math.Max(1, (int)Math.Round(minutesPerSample * 50))));
                    if (collectedUtc >= _Options.ToUtc) break;
                    SyntheticHostTelemetrySample host = CreateHostSample(client, collectedUtc, sampleIndex);
                    SyntheticProviderTelemetrySample provider = CreateProviderSample(client, host, sampleIndex);
                    SyntheticCollectionAttempt attempt = CreateAttempt(client, host);
                    batch.HostSamples.Add(host);
                    batch.ProviderSamples.Add(provider);
                    batch.Attempts.Add(attempt);
                }

                double jitter = 1.0 + _Random.Gaussian(0, 0.02 + _Options.Randomness * 0.16);
                cursor = cursor.AddMinutes(Math.Max(1, minutesPerSample * RandomSource.Clamp(jitter, 0.2, 2.2)));
                sampleIndex++;
            }
        }

        private SyntheticHostTelemetrySample CreateHostSample(SyntheticClientProfile client, DateTime collectedUtc, int sampleIndex)
        {
            double business = BusinessLoad(collectedUtc);
            double wave = Math.Sin((collectedUtc.Hour / 24.0) * Math.PI * 2 + client.Phase);
            double incident = IncidentLoad(collectedUtc);
            double variance = 3 + _Options.Randomness * 18;
            double cpu = RandomSource.Clamp(_Random.Gaussian(client.BaseCpuPercent + business * 11 + wave * 8 + incident * 0.65, variance), 1, 99);
            double memory = RandomSource.Clamp(_Random.Gaussian(client.BaseMemoryPercent + business * 5 + wave * 3 + incident * 0.25, 3 + _Options.Randomness * 8), 12, 98);
            double disk = RandomSource.Clamp(_Random.Gaussian(client.BaseDiskPercent + (sampleIndex / 800.0), 1.5 + _Options.Randomness * 4), 18, 96);
            double gpu = RandomSource.Clamp(_Random.Gaussian(client.BaseGpuPercent + business * 16 + wave * 10 + incident, variance + 3), 0, 100);
            DateTime receivedUtc = collectedUtc.AddMilliseconds(_Random.NextInt32(70, 1100));
            DateTime persistedUtc = receivedUtc.AddMilliseconds(_Random.NextInt32(4, 90));
            double elapsed = Math.Round(_Random.Gaussian(360, 120 + _Options.Randomness * 360), 1);
            string sampleId = SyntheticIdFactory.Create(SyntheticConstants.HostTelemetryIdPrefix, client.Id + collectedUtc.ToString("O", CultureInfo.InvariantCulture), 32);
            string attemptId = SyntheticIdFactory.Create(SyntheticConstants.CollectionAttemptIdPrefix, sampleId, 32);

            SyntheticHostTelemetrySample sample = new SyntheticHostTelemetrySample
            {
                Id = sampleId,
                CollectionRequestId = attemptId,
                Client = client,
                CollectedUtc = collectedUtc,
                ReceivedUtc = receivedUtc,
                PersistedUtc = persistedUtc,
                CollectionElapsedMs = RandomSource.Clamp(elapsed, 25, 3000),
                SampleAgeMsAtReceive = Math.Round((receivedUtc - collectedUtc).TotalMilliseconds, 1),
                CpuPercent = Math.Round(cpu, 1),
                MemoryPercent = Math.Round(memory, 1),
                DiskPercent = Math.Round(disk, 1),
                GpuPercent = Math.Round(gpu, 1)
            };
            sample.SnapshotJson = JsonUtil.Serialize(new
            {
                machineName = client.MachineName,
                collectedUtc = collectedUtc,
                operatingSystem = new { description = "Ubuntu 24.04.2 LTS", architecture = "X64" },
                cpu = new { processorCount = client.ProcessorCount, utilizationPercent = sample.CpuPercent },
                memory = new { totalBytes = client.MemoryGb * 1024L * 1024L * 1024L, utilizationPercent = sample.MemoryPercent },
                disk = new { totalBytes = client.DiskTb * 1024L * 1024L * 1024L * 1024L, utilizationPercent = sample.DiskPercent },
                gpu = new { provider = "NvidiaSmi", deviceCount = client.GpuCount, averageUtilizationPercent = sample.GpuPercent, name = client.GpuName },
                synthetic = SyntheticConstants.SyntheticMetadataValue
            });
            return sample;
        }

        private SyntheticProviderTelemetrySample CreateProviderSample(SyntheticClientProfile client, SyntheticHostTelemetrySample host, int sampleIndex)
        {
            double activeRequests = RandomSource.Clamp(Math.Round((host.GpuPercent / 100.0) * client.MaxParallelRequests + _Random.Gaussian(0, 0.65 + _Options.Randomness), 1), 0, client.MaxParallelRequests + 4);
            double queuedRequests = Math.Max(0, Math.Round(_Random.Gaussian(host.GpuPercent > 82 ? 2.4 : 0.25, 0.5 + _Options.Randomness * 1.8), 1));
            double latency = RandomSource.Clamp(_Random.Gaussian(client.BaseLatencyMs * (0.8 + host.GpuPercent / 150.0) + queuedRequests * 210, 120 + _Options.Randomness * 480), 80, 15000);
            string health = queuedRequests >= 3 || latency > client.BaseLatencyMs * 2.4 ? "Degraded" : "Healthy";
            string status = health == "Degraded" && _Random.Chance(0.18 + _Options.Randomness * 0.22) ? "Partial" : "Available";
            string sampleId = SyntheticIdFactory.Create(SyntheticConstants.ProviderTelemetryIdPrefix, client.Id + host.CollectedUtc.ToString("O", CultureInfo.InvariantCulture), 32);
            int loadedModelCount = Math.Max(1, Math.Min(client.Models.Count, (int)Math.Round((host.GpuPercent / 100.0) * client.Models.Count) + 1));

            SyntheticProviderTelemetrySample sample = new SyntheticProviderTelemetrySample
            {
                Id = sampleId,
                Client = client,
                CollectedUtc = host.CollectedUtc,
                ReceivedUtc = host.ReceivedUtc,
                PersistedUtc = host.PersistedUtc,
                Status = status,
                HealthStatus = health,
                ActiveRequests = activeRequests,
                QueuedRequests = queuedRequests,
                LatencyP95Ms = Math.Round(latency, 1),
                AvailableModelCount = client.Models.Count,
                LoadedModelCount = loadedModelCount
            };
            sample.SnapshotJson = JsonUtil.Serialize(new
            {
                providerName = client.ProviderName,
                apiType = client.ApiType,
                providerKind = client.ProviderKind,
                endpointDisplay = client.EndpointDisplay,
                status = sample.Status,
                healthStatus = sample.HealthStatus,
                activeRequests = sample.ActiveRequests,
                queuedRequests = sample.QueuedRequests,
                latencyP95Ms = sample.LatencyP95Ms,
                sampleIndex = sampleIndex,
                synthetic = SyntheticConstants.SyntheticMetadataValue
            });
            return sample;
        }

        private static SyntheticCollectionAttempt CreateAttempt(SyntheticClientProfile client, SyntheticHostTelemetrySample host)
        {
            return new SyntheticCollectionAttempt
            {
                Id = host.CollectionRequestId,
                Client = client,
                StartedUtc = host.CollectedUtc.AddMilliseconds(-40),
                CompletedUtc = host.CollectedUtc.AddMilliseconds(host.CollectionElapsedMs),
                ElapsedMs = host.CollectionElapsedMs,
                HostSampleId = host.Id,
                ProviderSampleCount = 1
            };
        }

        private double BusinessLoad(DateTime timestampUtc)
        {
            double localHour = (timestampUtc.Hour + 17) % 24;
            if (localHour >= 8 && localHour <= 18) return 1.0;
            if (localHour >= 19 && localHour <= 22) return 0.55;
            return 0.18;
        }

        private double IncidentLoad(DateTime timestampUtc)
        {
            bool testing = _Options.Scenario == "testing" || _Options.Scenario == "mixed";
            if (!testing && _Options.Randomness < 0.75) return 0;
            int dayMarker = timestampUtc.DayOfYear % 11;
            if (dayMarker == 3 && timestampUtc.Hour >= 17 && timestampUtc.Hour <= 20) return 10 + _Options.Randomness * 22;
            return 0;
        }

        private bool ShouldSkipSample(DateTime timestampUtc)
        {
            if (_Options.Scenario == "screenshot") return _Random.Chance(_Options.Randomness * 0.005);
            if (_Options.Scenario == "mixed") return _Random.Chance(0.006 + _Options.Randomness * 0.02);
            return _Random.Chance(0.015 + _Options.Randomness * 0.055);
        }
    }
}

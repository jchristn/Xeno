namespace Xeno.DataLoader
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Models;

    internal sealed class SyntheticDataLoader
    {
        private readonly DataLoaderOptions _Options;

        public SyntheticDataLoader(DataLoaderOptions options)
        {
            _Options = options ?? throw new ArgumentNullException(nameof(options));
            if (!_Options.Seed.HasValue)
            {
                _Options.Seed = Environment.TickCount ^ DateTime.UtcNow.Millisecond;
            }
        }

        public async Task<SyntheticDataLoadResult> RunAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            int seed = _Options.Seed ?? throw new InvalidOperationException("Synthetic data generation seed was not initialized.");
            RandomSource random = new RandomSource(seed);
            SyntheticClientFactory clientFactory = new SyntheticClientFactory(_Options, random);
            List<SyntheticClientProfile> clients = clientFactory.CreateClients();

            SyntheticRequestFactory requestFactory = new SyntheticRequestFactory(_Options, random, clients);
            List<RequestHistoryEntry> requestHistory = requestFactory.CreateRequestHistory(token);

            SyntheticTelemetryFactory telemetryFactory = new SyntheticTelemetryFactory(_Options, random, clients);
            SyntheticTelemetryBatch telemetry = telemetryFactory.CreateTelemetry(token);

            SqliteSyntheticDataStore store = new SqliteSyntheticDataStore(_Options);
            return await store.LoadAsync(clients, requestHistory, telemetry, token).ConfigureAwait(false);
        }
    }
}

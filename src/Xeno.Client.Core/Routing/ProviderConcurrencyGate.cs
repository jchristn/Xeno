namespace Xeno.Client.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Client.Services;

    /// <summary>
    /// Per-provider and per-model concurrency limits shared by network requests the worker serves and local requests the
    /// listener routes to the same provider. Limits are read from settings on every acquire, so changes apply without a restart.
    /// A request needs a free slot at its model's limit (when one is set) and at its provider's limit.
    /// Thread safe.
    /// </summary>
    public sealed class ProviderConcurrencyGate
    {
        private static readonly ProviderConcurrencyGate _Shared = new ProviderConcurrencyGate();
        private readonly object _Lock = new object();
        private readonly Dictionary<string, ProviderConcurrencyState> _Providers = new Dictionary<string, ProviderConcurrencyState>(StringComparer.OrdinalIgnoreCase);
        private TaskCompletionSource<bool> _Released = NewSignal();

        /// <summary>
        /// The gate the worker and the local inference listener share in one client process.
        /// </summary>
        public static ProviderConcurrencyGate Shared
        {
            get
            {
                return _Shared;
            }
        }

        /// <summary>
        /// Try to take a slot for a network request without waiting.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="provider">Provider.</param>
        /// <param name="model">Model, or null.</param>
        /// <returns>A lease, or null when the provider or model is at its limit.</returns>
        /// <exception cref="ArgumentNullException">settings or provider is null.</exception>
        public ConcurrencyLease? TryAcquireNetwork(ClientSettings settings, ClientRunnerSettings provider, string? model)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(provider);
            ConcurrencyLimits limits = ConcurrencyLimitResolver.Resolve(settings, provider, model);
            string key = LocalModelNameNormalizer.Normalize(model);
            lock (_Lock)
            {
                ProviderConcurrencyState state = State(provider.Name);
                if (TryTake(state, limits, key, false)) return new ConcurrencyLease(this, provider.Name, key, false, 0);
                state.NetworkRejected++;
                return null;
            }
        }

        /// <summary>
        /// Take a slot for a local request, waiting up to <paramref name="waitMs"/> for one to free up.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="provider">Provider.</param>
        /// <param name="model">Model, or null.</param>
        /// <param name="waitMs">Maximum wait in milliseconds. 0 does not wait.</param>
        /// <param name="token">Cancellation token. Cancelling while queued gives up the place in line and throws.</param>
        /// <returns>A lease, or null when no slot freed up in time.</returns>
        /// <exception cref="ArgumentNullException">settings or provider is null.</exception>
        /// <exception cref="OperationCanceledException">The token was cancelled while waiting.</exception>
        public async Task<ConcurrencyLease?> AcquireLocalAsync(ClientSettings settings, ClientRunnerSettings provider, string? model, int waitMs, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(provider);
            string key = LocalModelNameNormalizer.Normalize(model);
            Stopwatch waited = Stopwatch.StartNew();
            bool queued = false;
            try
            {
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    ConcurrencyLimits limits = ConcurrencyLimitResolver.Resolve(settings, provider, model);
                    Task signal;
                    lock (_Lock)
                    {
                        ProviderConcurrencyState state = State(provider.Name);
                        if (TryTake(state, limits, key, true))
                        {
                            if (queued) state.Queued--;
                            queued = false;
                            return new ConcurrencyLease(this, provider.Name, key, true, waited.Elapsed.TotalMilliseconds);
                        }

                        double remaining = waitMs - waited.Elapsed.TotalMilliseconds;
                        if (remaining <= 0)
                        {
                            if (queued) state.Queued--;
                            queued = false;
                            state.LocalRejected++;
                            return null;
                        }

                        if (!queued)
                        {
                            state.Queued++;
                            queued = true;
                        }

                        signal = _Released.Task;
                    }

                    int delay = (int)Math.Max(1, Math.Min(waitMs - waited.Elapsed.TotalMilliseconds, 250));
                    await Task.WhenAny(signal, Task.Delay(delay, token)).ConfigureAwait(false);
                }
            }
            finally
            {
                if (queued)
                {
                    lock (_Lock)
                    {
                        State(provider.Name).Queued--;
                    }
                }
            }
        }

        /// <summary>
        /// Local requests holding a slot on providers that are shared with the network. The client subtracts these from the capacity it reports to the server.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <returns>Count.</returns>
        /// <exception cref="ArgumentNullException">settings is null.</exception>
        public int LocalInFlightOnSharedProviders(ClientSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            HashSet<string> shared = new HashSet<string>(settings.Providers.Where(item => item != null && item.ShareWithNetwork).Select(item => item.Name), StringComparer.OrdinalIgnoreCase);
            lock (_Lock)
            {
                return _Providers.Where(item => shared.Contains(item.Key)).Sum(item => item.Value.InFlightLocal);
            }
        }

        /// <summary>
        /// Counters for every provider the gate has seen, with limits resolved from the given settings.
        /// </summary>
        /// <param name="settings">Client settings, used for the current limits.</param>
        /// <returns>Snapshots, never null.</returns>
        /// <exception cref="ArgumentNullException">settings is null.</exception>
        public List<ProviderConcurrencySnapshot> Snapshot(ClientSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            List<ProviderConcurrencySnapshot> snapshots = new List<ProviderConcurrencySnapshot>();
            lock (_Lock)
            {
                foreach (ClientRunnerSettings provider in settings.Providers.Where(item => item != null))
                {
                    _Providers.TryGetValue(provider.Name, out ProviderConcurrencyState? state);
                    snapshots.Add(new ProviderConcurrencySnapshot
                    {
                        Provider = provider.Name,
                        InFlight = state == null ? 0 : state.InFlightLocal + state.InFlightNetwork,
                        InFlightLocal = state?.InFlightLocal ?? 0,
                        InFlightNetwork = state?.InFlightNetwork ?? 0,
                        Queued = Math.Max(0, state?.Queued ?? 0),
                        Limit = ConcurrencyLimitResolver.Resolve(settings, provider, null).ProviderLimit,
                        LocalRejected = state?.LocalRejected ?? 0,
                        NetworkRejected = state?.NetworkRejected ?? 0
                    });
                }
            }

            return snapshots;
        }

        /// <summary>
        /// Clear every counter. For tests and for a client restart inside one process.
        /// </summary>
        public void Reset()
        {
            lock (_Lock)
            {
                _Providers.Clear();
                TaskCompletionSource<bool> previous = _Released;
                _Released = NewSignal();
                previous.TrySetResult(true);
            }
        }

        internal void Release(ConcurrencyLease lease)
        {
            TaskCompletionSource<bool> previous;
            lock (_Lock)
            {
                ProviderConcurrencyState state = State(lease.Provider);
                if (lease.Local) state.InFlightLocal = Math.Max(0, state.InFlightLocal - 1);
                else state.InFlightNetwork = Math.Max(0, state.InFlightNetwork - 1);
                if (state.Models.TryGetValue(lease.Model, out int count)) state.Models[lease.Model] = Math.Max(0, count - 1);
                previous = _Released;
                _Released = NewSignal();
            }

            previous.TrySetResult(true);
        }

        private ProviderConcurrencyState State(string provider)
        {
            if (!_Providers.TryGetValue(provider, out ProviderConcurrencyState? state))
            {
                state = new ProviderConcurrencyState();
                _Providers[provider] = state;
            }

            return state;
        }

        private static bool TryTake(ProviderConcurrencyState state, ConcurrencyLimits limits, string model, bool local)
        {
            if (state.InFlightLocal + state.InFlightNetwork >= limits.ProviderLimit) return false;
            state.Models.TryGetValue(model, out int modelInFlight);
            if (limits.ModelLimit.HasValue && modelInFlight >= limits.ModelLimit.Value) return false;
            if (local) state.InFlightLocal++;
            else state.InFlightNetwork++;
            state.Models[model] = modelInFlight + 1;
            return true;
        }

        private static TaskCompletionSource<bool> NewSignal()
        {
            return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}

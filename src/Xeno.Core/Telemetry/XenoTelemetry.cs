namespace Xeno.Core.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Reflection;
    using System.Threading;

    /// <summary>
    /// Static telemetry registry for Xeno. Instruments are created once at type load
    /// and remain inert until an OpenTelemetry provider begins listening to the
    /// <see cref="MeterName"/> meter or <see cref="ActivitySourceName"/> activity source.
    /// All record methods are safe to call regardless of whether telemetry is enabled.
    /// </summary>
    public static class XenoTelemetry
    {
        #region Public-Members

        /// <summary>
        /// Meter name. Also used as the OpenTelemetry activity source name.
        /// </summary>
        public const string MeterName = "Xeno";

        /// <summary>
        /// Activity source name.
        /// </summary>
        public const string ActivitySourceName = "Xeno";

        /// <summary>
        /// Activity source for distributed tracing.
        /// </summary>
        public static ActivitySource ActivitySource
        {
            get => _ActivitySource;
        }

        /// <summary>
        /// True when a state provider has been registered and observable gauges will report values.
        /// </summary>
        public static bool StateRegistered
        {
            get => _PlatformSnapshot != null;
        }

        #endregion

        #region Private-Members

        private static readonly Meter _Meter = new Meter(MeterName, ResolveVersion());
        private static readonly ActivitySource _ActivitySource = new ActivitySource(ActivitySourceName, ResolveVersion());

        // Request / relay counters and histograms.
        private static readonly Histogram<double> _AnalyticsQueryDuration = _Meter.CreateHistogram<double>(
            "xeno.analytics.query.duration", "s", "Usage analytics query duration.");
        private static readonly Counter<long> _AnalyticsRecordsScanned = _Meter.CreateCounter<long>(
            "xeno.analytics.records.scanned", "{record}", "Records read by usage analytics queries.");

        private static readonly Counter<long> _HttpRequests = _Meter.CreateCounter<long>(
            "xeno.http.requests", "{request}", "Total HTTP requests handled by the server.");
        private static readonly Histogram<double> _HttpRequestDuration = _Meter.CreateHistogram<double>(
            "xeno.http.request.duration", "s", "HTTP request handling duration.");

        private static readonly Counter<long> _RelayRequests = _Meter.CreateCounter<long>(
            "xeno.relay.requests", "{request}", "Total inference requests relayed to the fleet.");
        private static readonly Counter<long> _RelayFailures = _Meter.CreateCounter<long>(
            "xeno.relay.failures", "{request}", "Total relayed requests that ended in failure.");
        private static readonly Histogram<double> _RelayRoundTrip = _Meter.CreateHistogram<double>(
            "xeno.relay.roundtrip.duration", "s", "End-to-end relayed request duration.");
        private static readonly Histogram<double> _RelayUpstream = _Meter.CreateHistogram<double>(
            "xeno.relay.upstream.duration", "s", "Client-to-provider upstream duration.");
        private static readonly Histogram<double> _RelayTimeToFirstToken = _Meter.CreateHistogram<double>(
            "xeno.relay.time_to_first_token", "s", "Time from request start to first streamed token.");
        private static readonly Histogram<double> _RelayStreaming = _Meter.CreateHistogram<double>(
            "xeno.relay.streaming.duration", "s", "Total streaming duration for streamed responses.");
        private static readonly Histogram<long> _RelayRequestBytes = _Meter.CreateHistogram<long>(
            "xeno.relay.request.size", "By", "Relayed request payload size.");
        private static readonly Histogram<long> _RelayResponseBytes = _Meter.CreateHistogram<long>(
            "xeno.relay.response.size", "By", "Relayed response payload size.");

        // Token counters and per-request histogram.
        private static readonly Counter<long> _PromptTokens = _Meter.CreateCounter<long>(
            "xeno.tokens.prompt", "{token}", "Total prompt tokens processed.");
        private static readonly Counter<long> _CompletionTokens = _Meter.CreateCounter<long>(
            "xeno.tokens.completion", "{token}", "Total completion tokens processed.");
        private static readonly Counter<long> _TotalTokens = _Meter.CreateCounter<long>(
            "xeno.tokens.all", "{token}", "Total tokens processed.");
        private static readonly Histogram<long> _TokensPerRequest = _Meter.CreateHistogram<long>(
            "xeno.tokens.per_request", "{token}", "Total tokens per relayed request.");
        private static readonly Counter<long> _StreamChunks = _Meter.CreateCounter<long>(
            "xeno.relay.stream.chunks", "{chunk}", "Total streamed chunks emitted.");
        private static readonly Counter<long> _StreamEvents = _Meter.CreateCounter<long>(
            "xeno.relay.stream.events", "{event}", "Total streamed events emitted.");

        // Connection counters.
        private static readonly Counter<long> _ClientConnections = _Meter.CreateCounter<long>(
            "xeno.client.connections", "{connection}", "Total client websocket connections accepted.");
        private static readonly Counter<long> _ClientDisconnections = _Meter.CreateCounter<long>(
            "xeno.client.disconnections", "{connection}", "Total client websocket disconnections.");
        private static readonly Counter<long> _ClientRegistrations = _Meter.CreateCounter<long>(
            "xeno.client.registrations", "{registration}", "Total client registrations.");
        private static readonly Counter<long> _ClientEvictions = _Meter.CreateCounter<long>(
            "xeno.client.evictions", "{eviction}", "Total stale client connections evicted.");

        // Auth counters.
        private static readonly Counter<long> _AuthAttempts = _Meter.CreateCounter<long>(
            "xeno.auth.attempts", "{attempt}", "Total authentication attempts.");
        private static readonly Counter<long> _AuthorizationDenials = _Meter.CreateCounter<long>(
            "xeno.auth.denials", "{denial}", "Total authorization denials.");
        private static readonly Counter<long> _PrivilegedBypasses = _Meter.CreateCounter<long>(
            "xeno.auth.privileged_bypasses", "{bypass}", "Total privileged authorization bypasses.");

        // Data mutation counter.
        private static readonly Counter<long> _RecordMutations = _Meter.CreateCounter<long>(
            "xeno.data.mutations", "{mutation}", "Total create/update/delete record mutations.");

        // Email counters.
        private static readonly Counter<long> _EmailResults = _Meter.CreateCounter<long>(
            "xeno.email.results", "{email}", "Total email notification outcomes.");

        // Ledger counters.
        private static readonly Counter<long> _LedgerEvents = _Meter.CreateCounter<long>(
            "xeno.ledger.events", "{event}", "Total ledger usage events recorded.");

        // Fleet telemetry collection counters.
        private static readonly Counter<long> _TelemetryCollectionCycles = _Meter.CreateCounter<long>(
            "xeno.fleet.collection.cycles", "{cycle}", "Total fleet telemetry collection cycles.");
        private static readonly Counter<long> _TelemetryCollectionFailures = _Meter.CreateCounter<long>(
            "xeno.fleet.collection.failures", "{failure}", "Total fleet telemetry collection failures.");

        // Request history retention counter.
        private static readonly Counter<long> _RequestHistoryPruned = _Meter.CreateCounter<long>(
            "xeno.request_history.pruned", "{record}", "Total request-history records pruned by retention.");

        // Observable gauge state.
        private static Func<PlatformGaugeSnapshot>? _PlatformSnapshot = null;
        private static Func<TelemetryFleetSnapshot>? _FleetSnapshot = null;
        private static bool _IncludeTenantLabel = false;

        private static readonly Process _Process = Process.GetCurrentProcess();
        private static readonly DateTime _StartUtc = DateTime.UtcNow;
        private static long _TicksAnchor = 0;

        #endregion

        #region Constructors-and-Factories

        static XenoTelemetry()
        {
            _TicksAnchor = _StartUtc.Ticks;

            _Meter.CreateObservableGauge<long>(
                "xeno.build.info", ObserveBuildInfo, null, "Static build information, always 1.");

            _Meter.CreateObservableGauge<double>(
                "xeno.process.memory.working_set", ObserveWorkingSet, "By", "Process working set memory.");
            _Meter.CreateObservableGauge<double>(
                "xeno.process.memory.gc_heap", ObserveGcHeap, "By", "Managed GC heap size.");
            _Meter.CreateObservableCounter<double>(
                "xeno.process.cpu.time", ObserveCpuTime, "s", "Total processor time consumed by the process.");
            _Meter.CreateObservableGauge<int>(
                "xeno.process.threads", ObserveThreadCount, "{thread}", "Process thread count.");
            _Meter.CreateObservableGauge<double>(
                "xeno.process.uptime", ObserveUptime, "s", "Process uptime.");

            _Meter.CreateObservableGauge<int>(
                "xeno.clients.connected", ObserveConnectedClients, "{client}", "Currently connected clients.");
            _Meter.CreateObservableGauge<int>(
                "xeno.models.available", ObserveAvailableModels, "{model}", "Distinct models advertised by the fleet.");
            _Meter.CreateObservableGauge<int>(
                "xeno.relay.inflight", ObserveInflightRequests, "{request}", "In-flight relayed requests.");
            _Meter.CreateObservableGauge<int>(
                "xeno.ledger.queue.depth", ObserveLedgerQueueDepth, "{event}", "Ledger queue depth.");

            _Meter.CreateObservableGauge<double>(
                "xeno.fleet.host.cpu", ObserveFleetHostCpu, "%", "Per-client host CPU utilization.");
            _Meter.CreateObservableGauge<double>(
                "xeno.fleet.host.memory", ObserveFleetHostMemory, "%", "Per-client host memory utilization.");
            _Meter.CreateObservableGauge<double>(
                "xeno.fleet.host.disk", ObserveFleetHostDisk, "%", "Per-client host disk utilization.");
            _Meter.CreateObservableGauge<double>(
                "xeno.fleet.host.gpu", ObserveFleetHostGpu, "%", "Per-client host GPU utilization.");
            _Meter.CreateObservableGauge<int>(
                "xeno.fleet.provider.models_available", ObserveFleetProviderModelsAvailable, "{model}", "Per-provider available models.");
            _Meter.CreateObservableGauge<int>(
                "xeno.fleet.provider.models_loaded", ObserveFleetProviderModelsLoaded, "{model}", "Per-provider loaded models.");
            _Meter.CreateObservableGauge<int>(
                "xeno.fleet.provider.up", ObserveFleetProviderUp, "{provider}", "Per-provider reachability (1 up, 0 down).");
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Register the live state accessors used by observable gauges. Called once during
        /// server startup after the platform is available.
        /// </summary>
        /// <param name="platformSnapshot">Platform snapshot accessor.</param>
        /// <param name="fleetSnapshot">Fleet snapshot accessor.</param>
        /// <param name="includeTenantLabel">Whether tenant labeling is enabled.</param>
        public static void RegisterState(
            Func<PlatformGaugeSnapshot> platformSnapshot,
            Func<TelemetryFleetSnapshot> fleetSnapshot,
            bool includeTenantLabel)
        {
            _PlatformSnapshot = platformSnapshot ?? throw new ArgumentNullException(nameof(platformSnapshot));
            _FleetSnapshot = fleetSnapshot ?? throw new ArgumentNullException(nameof(fleetSnapshot));
            _IncludeTenantLabel = includeTenantLabel;
        }

        /// <summary>
        /// Clear registered state accessors. Observable gauges report nothing afterward.
        /// </summary>
        public static void ClearState()
        {
            _PlatformSnapshot = null;
            _FleetSnapshot = null;
        }

        /// <summary>
        /// Record a completed HTTP request.
        /// </summary>
        /// <param name="method">HTTP method.</param>
        /// <param name="route">Bounded route template (never a raw path).</param>
        /// <param name="statusCode">HTTP status code.</param>
        /// <param name="durationSeconds">Handling duration in seconds.</param>
        public static void RecordHttpRequest(string method, string route, int statusCode, double durationSeconds)
        {
            KeyValuePair<string, object?>[] tags = new KeyValuePair<string, object?>[]
            {
                new KeyValuePair<string, object?>("http.method", NormalizeMethod(method)),
                new KeyValuePair<string, object?>("http.route", string.IsNullOrEmpty(route) ? "unknown" : route),
                new KeyValuePair<string, object?>("http.status_code", statusCode),
                new KeyValuePair<string, object?>("http.status_class", StatusClass(statusCode))
            };

            _HttpRequests.Add(1, tags);
            if (durationSeconds >= 0.0) _HttpRequestDuration.Record(durationSeconds, tags);
        }

        /// <summary>
        /// Record a completed relayed inference request and its associated token, timing, and size measurements.
        /// </summary>
        /// <param name="sample">Relay measurement sample.</param>
        public static void RecordRelay(RelayMetricSample sample)
        {
            if (sample == null) return;

            KeyValuePair<string, object?>[] tags = BuildRelayTags(sample);

            _RelayRequests.Add(1, tags);
            bool success = string.Equals(sample.Outcome, "success", StringComparison.OrdinalIgnoreCase);
            if (!success) _RelayFailures.Add(1, tags);

            if (sample.RoundTripSeconds.HasValue && sample.RoundTripSeconds.Value >= 0.0)
                _RelayRoundTrip.Record(sample.RoundTripSeconds.Value, tags);
            if (sample.UpstreamSeconds.HasValue && sample.UpstreamSeconds.Value >= 0.0)
                _RelayUpstream.Record(sample.UpstreamSeconds.Value, tags);
            if (sample.TimeToFirstTokenSeconds.HasValue && sample.TimeToFirstTokenSeconds.Value >= 0.0)
                _RelayTimeToFirstToken.Record(sample.TimeToFirstTokenSeconds.Value, tags);
            if (sample.StreamingSeconds.HasValue && sample.StreamingSeconds.Value >= 0.0)
                _RelayStreaming.Record(sample.StreamingSeconds.Value, tags);
            if (sample.RequestBytes.HasValue && sample.RequestBytes.Value >= 0)
                _RelayRequestBytes.Record(sample.RequestBytes.Value, tags);
            if (sample.ResponseBytes.HasValue && sample.ResponseBytes.Value >= 0)
                _RelayResponseBytes.Record(sample.ResponseBytes.Value, tags);

            if (sample.PromptTokens.HasValue && sample.PromptTokens.Value > 0)
                _PromptTokens.Add(sample.PromptTokens.Value, tags);
            if (sample.CompletionTokens.HasValue && sample.CompletionTokens.Value > 0)
                _CompletionTokens.Add(sample.CompletionTokens.Value, tags);
            if (sample.TotalTokens.HasValue && sample.TotalTokens.Value > 0)
            {
                _TotalTokens.Add(sample.TotalTokens.Value, tags);
                _TokensPerRequest.Record(sample.TotalTokens.Value, tags);
            }
            if (sample.StreamChunks.HasValue && sample.StreamChunks.Value > 0)
                _StreamChunks.Add(sample.StreamChunks.Value, tags);
            if (sample.StreamEvents.HasValue && sample.StreamEvents.Value > 0)
                _StreamEvents.Add(sample.StreamEvents.Value, tags);
        }

        /// <summary>
        /// Record a client websocket connection.
        /// </summary>
        /// <param name="provider">Client provider type, or null.</param>
        public static void RecordClientConnection(string? provider)
        {
            _ClientConnections.Add(1, ProviderTag(provider));
        }

        /// <summary>
        /// Record a client websocket disconnection.
        /// </summary>
        /// <param name="reason">Bounded disconnect reason.</param>
        public static void RecordClientDisconnection(string reason)
        {
            _ClientDisconnections.Add(1, new KeyValuePair<string, object?>("reason", string.IsNullOrEmpty(reason) ? "unknown" : reason));
        }

        /// <summary>
        /// Record a client registration.
        /// </summary>
        public static void RecordClientRegistration()
        {
            _ClientRegistrations.Add(1);
        }

        /// <summary>
        /// Record eviction of a stale client connection.
        /// </summary>
        /// <param name="count">Number of connections evicted.</param>
        public static void RecordClientEvictions(long count)
        {
            if (count > 0) _ClientEvictions.Add(count);
        }

        /// <summary>
        /// Record an authentication attempt.
        /// </summary>
        /// <param name="success">Whether authentication succeeded.</param>
        public static void RecordAuthAttempt(bool success)
        {
            _AuthAttempts.Add(1, new KeyValuePair<string, object?>("result", success ? "success" : "failure"));
        }

        /// <summary>
        /// Record an authorization denial.
        /// </summary>
        public static void RecordAuthorizationDenial()
        {
            _AuthorizationDenials.Add(1);
        }

        /// <summary>
        /// Record a privileged authorization bypass.
        /// </summary>
        public static void RecordPrivilegedBypass()
        {
            _PrivilegedBypasses.Add(1);
        }

        /// <summary>
        /// Record a data record mutation.
        /// </summary>
        /// <param name="entity">Bounded entity type name.</param>
        /// <param name="operation">Operation: "create", "update", or "delete".</param>
        public static void RecordMutation(string entity, string operation)
        {
            _RecordMutations.Add(1,
                new KeyValuePair<string, object?>("entity", string.IsNullOrEmpty(entity) ? "unknown" : entity),
                new KeyValuePair<string, object?>("operation", string.IsNullOrEmpty(operation) ? "unknown" : operation));
        }

        /// <summary>
        /// Record an email notification outcome.
        /// </summary>
        /// <param name="outcome">Outcome: "sent", "skipped", or "failed".</param>
        public static void RecordEmail(string outcome)
        {
            _EmailResults.Add(1, new KeyValuePair<string, object?>("outcome", string.IsNullOrEmpty(outcome) ? "unknown" : outcome));
        }

        /// <summary>
        /// Record a ledger usage event.
        /// </summary>
        /// <param name="outcome">Outcome: "recorded", "skipped", or "failed".</param>
        public static void RecordLedgerEvent(string outcome)
        {
            _LedgerEvents.Add(1, new KeyValuePair<string, object?>("outcome", string.IsNullOrEmpty(outcome) ? "unknown" : outcome));
        }

        /// <summary>
        /// Record a fleet telemetry collection cycle outcome.
        /// </summary>
        /// <param name="success">Whether the cycle succeeded.</param>
        public static void RecordTelemetryCollectionCycle(bool success)
        {
            _TelemetryCollectionCycles.Add(1);
            if (!success) _TelemetryCollectionFailures.Add(1);
        }

        /// <summary>
        /// Record one usage analytics query. Best effort; never throws.
        /// </summary>
        /// <param name="dataset">Dataset name (low cardinality: one of four values).</param>
        /// <param name="outcome">ok, truncated, or error.</param>
        /// <param name="seconds">Duration in seconds.</param>
        /// <param name="records">Records scanned.</param>
        public static void RecordAnalyticsQuery(string dataset, string outcome, double seconds, long records)
        {
            try
            {
                KeyValuePair<string, object?>[] tags = { new KeyValuePair<string, object?>("dataset", dataset), new KeyValuePair<string, object?>("outcome", outcome) };
                _AnalyticsQueryDuration.Record(seconds, tags);
                if (records > 0) _AnalyticsRecordsScanned.Add(records, new KeyValuePair<string, object?>("dataset", dataset));
            }
            catch (InvalidOperationException)
            {
            }
        }

        /// <summary>
        /// Record pruning of request-history records by retention.
        /// </summary>
        /// <param name="count">Number of records pruned.</param>
        public static void RecordRequestHistoryPruned(long count)
        {
            if (count > 0) _RequestHistoryPruned.Add(count);
        }

        #endregion

        #region Private-Methods

        private static KeyValuePair<string, object?>[] BuildRelayTags(RelayMetricSample sample)
        {
            List<KeyValuePair<string, object?>> tags = new List<KeyValuePair<string, object?>>(6)
            {
                new KeyValuePair<string, object?>("model", string.IsNullOrEmpty(sample.Model) ? "unknown" : sample.Model),
                new KeyValuePair<string, object?>("client_id", string.IsNullOrEmpty(sample.ClientId) ? "unknown" : sample.ClientId),
                new KeyValuePair<string, object?>("provider", string.IsNullOrEmpty(sample.Provider) ? "unknown" : sample.Provider),
                new KeyValuePair<string, object?>("outcome", string.IsNullOrEmpty(sample.Outcome) ? "unknown" : sample.Outcome),
                new KeyValuePair<string, object?>("streamed", sample.Streamed)
            };

            if (_IncludeTenantLabel && !string.IsNullOrEmpty(sample.Tenant))
                tags.Add(new KeyValuePair<string, object?>("tenant", sample.Tenant));

            return tags.ToArray();
        }

        private static KeyValuePair<string, object?>[] ProviderTag(string? provider)
        {
            return new KeyValuePair<string, object?>[]
            {
                new KeyValuePair<string, object?>("provider", string.IsNullOrEmpty(provider) ? "unknown" : provider)
            };
        }

        private static string NormalizeMethod(string method)
        {
            if (string.IsNullOrEmpty(method)) return "unknown";

            switch (method.ToUpperInvariant())
            {
                case "GET":
                case "PUT":
                case "POST":
                case "DELETE":
                case "HEAD":
                case "PATCH":
                case "OPTIONS":
                    return method.ToUpperInvariant();
                default:
                    return "other";
            }
        }

        private static string StatusClass(int statusCode)
        {
            if (statusCode >= 200 && statusCode < 300) return "2xx";
            if (statusCode >= 300 && statusCode < 400) return "3xx";
            if (statusCode >= 400 && statusCode < 500) return "4xx";
            if (statusCode >= 500 && statusCode < 600) return "5xx";
            return "other";
        }

        private static string ResolveVersion()
        {
            Version? version = Assembly.GetExecutingAssembly().GetName().Version;
            return version != null ? version.ToString() : "0.0.0";
        }

        private static Measurement<long> ObserveBuildInfo()
        {
            return new Measurement<long>(1,
                new KeyValuePair<string, object?>("version", ResolveVersion()),
                new KeyValuePair<string, object?>("runtime", Environment.Version.ToString()));
        }

        private static double ObserveWorkingSet()
        {
            _Process.Refresh();
            return _Process.WorkingSet64;
        }

        private static double ObserveGcHeap()
        {
            return GC.GetTotalMemory(false);
        }

        private static double ObserveCpuTime()
        {
            return _Process.TotalProcessorTime.TotalSeconds;
        }

        private static int ObserveThreadCount()
        {
            _Process.Refresh();
            return _Process.Threads.Count;
        }

        private static double ObserveUptime()
        {
            return (DateTime.UtcNow - _StartUtc).TotalSeconds;
        }

        private static int ObserveConnectedClients()
        {
            Func<PlatformGaugeSnapshot>? f = _PlatformSnapshot;
            return f != null ? f().ConnectedClients : 0;
        }

        private static int ObserveAvailableModels()
        {
            Func<PlatformGaugeSnapshot>? f = _PlatformSnapshot;
            return f != null ? f().AvailableModels : 0;
        }

        private static int ObserveInflightRequests()
        {
            Func<PlatformGaugeSnapshot>? f = _PlatformSnapshot;
            return f != null ? f().InflightRequests : 0;
        }

        private static int ObserveLedgerQueueDepth()
        {
            Func<PlatformGaugeSnapshot>? f = _PlatformSnapshot;
            return f != null ? f().LedgerQueueDepth : 0;
        }

        private static IEnumerable<Measurement<double>> ObserveFleetHostCpu()
        {
            return ObserveFleetHost(host => host.CpuPercent);
        }

        private static IEnumerable<Measurement<double>> ObserveFleetHostMemory()
        {
            return ObserveFleetHost(host => host.MemoryPercent);
        }

        private static IEnumerable<Measurement<double>> ObserveFleetHostDisk()
        {
            return ObserveFleetHost(host => host.DiskPercent);
        }

        private static IEnumerable<Measurement<double>> ObserveFleetHostGpu()
        {
            return ObserveFleetHost(host => host.GpuPercent);
        }

        private static IEnumerable<Measurement<double>> ObserveFleetHost(Func<FleetHostGauge, double?> selector)
        {
            Func<TelemetryFleetSnapshot>? f = _FleetSnapshot;
            if (f == null) yield break;

            TelemetryFleetSnapshot snapshot = f();
            foreach (FleetHostGauge host in snapshot.Hosts)
            {
                double? value = selector(host);
                if (!value.HasValue) continue;
                yield return new Measurement<double>(value.Value,
                    new KeyValuePair<string, object?>("client_id", string.IsNullOrEmpty(host.ClientId) ? "unknown" : host.ClientId));
            }
        }

        private static IEnumerable<Measurement<int>> ObserveFleetProviderModelsAvailable()
        {
            return ObserveFleetProviderInt(p => p.AvailableModels, false);
        }

        private static IEnumerable<Measurement<int>> ObserveFleetProviderModelsLoaded()
        {
            return ObserveFleetProviderInt(p => p.LoadedModels, true);
        }

        private static IEnumerable<Measurement<int>> ObserveFleetProviderUp()
        {
            return ObserveFleetProviderInt(p => p.Up ? 1 : 0, false);
        }

        private static IEnumerable<Measurement<int>> ObserveFleetProviderInt(Func<FleetProviderGauge, int> selector, bool skipNegative)
        {
            Func<TelemetryFleetSnapshot>? f = _FleetSnapshot;
            if (f == null) yield break;

            TelemetryFleetSnapshot snapshot = f();
            foreach (FleetProviderGauge provider in snapshot.Providers)
            {
                int value = selector(provider);
                if (skipNegative && value < 0) continue;
                yield return new Measurement<int>(value,
                    new KeyValuePair<string, object?>("client_id", string.IsNullOrEmpty(provider.ClientId) ? "unknown" : provider.ClientId),
                    new KeyValuePair<string, object?>("provider", string.IsNullOrEmpty(provider.Provider) ? "unknown" : provider.Provider));
            }
        }

        #endregion
    }
}

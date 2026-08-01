namespace Xeno.Core.Telemetry
{
    using System;
    using SyslogLogging;
    using OpenTelemetry;
    using OpenTelemetry.Exporter;
    using OpenTelemetry.Metrics;
    using OpenTelemetry.Resources;
    using OpenTelemetry.Trace;
    using Xeno.Core.Settings;

    /// <summary>
    /// Owns the OpenTelemetry meter and tracer providers for the lifetime of the server.
    /// When telemetry is disabled the Xeno instruments remain inert and no exporter is created.
    /// </summary>
    public class TelemetryService : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// True when a metrics or tracer provider is active.
        /// </summary>
        public bool Active
        {
            get => _MeterProvider != null || _TracerProvider != null;
        }

        #endregion

        #region Private-Members

        private const string _Header = "[TelemetryService] ";

        private readonly TelemetrySettings _Settings;
        private readonly LoggingModule? _Logging;

        private MeterProvider? _MeterProvider = null;
        private TracerProvider? _TracerProvider = null;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate and build the configured OpenTelemetry providers.
        /// </summary>
        /// <param name="settings">Telemetry settings.</param>
        /// <param name="logging">Logging module, or null.</param>
        public TelemetryService(TelemetrySettings settings, LoggingModule? logging)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging;

            if (!_Settings.Enable)
            {
                _Logging?.Debug(_Header + "telemetry disabled, no providers created");
                return;
            }

            ResourceBuilder resourceBuilder = BuildResource();

            if (_Settings.Metrics.Enable) BuildMeterProvider(resourceBuilder);
            if (_Settings.Traces.Enable) BuildTracerProvider(resourceBuilder);

            _Logging?.Debug(_Header + "telemetry initialized (metrics=" + _Settings.Metrics.Enable + " traces=" + _Settings.Traces.Enable + ")");
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Dispose.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Private-Methods

        private ResourceBuilder BuildResource()
        {
            return ResourceBuilder.CreateDefault()
                .AddService(
                    serviceName: string.IsNullOrEmpty(_Settings.ServiceName) ? "xeno-server" : _Settings.ServiceName,
                    serviceVersion: ResolveVersion());
        }

        private void BuildMeterProvider(ResourceBuilder resourceBuilder)
        {
            MeterProviderBuilder builder = Sdk.CreateMeterProviderBuilder()
                .SetResourceBuilder(resourceBuilder)
                .AddMeter(XenoTelemetry.MeterName)
                .AddRuntimeInstrumentation();

            AddHistogramViews(builder);

            builder.AddOtlpExporter((exporterOptions, readerOptions) =>
            {
                ConfigureOtlp(exporterOptions);
                readerOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = _Settings.Metrics.ExportIntervalMs;
            });

            _MeterProvider = builder.Build();
        }

        private void BuildTracerProvider(ResourceBuilder resourceBuilder)
        {
            _TracerProvider = Sdk.CreateTracerProviderBuilder()
                .SetResourceBuilder(resourceBuilder)
                .AddSource(XenoTelemetry.ActivitySourceName)
                .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(_Settings.Traces.SamplingRatio)))
                .AddOtlpExporter(ConfigureOtlp)
                .Build();
        }

        private static void AddHistogramViews(MeterProviderBuilder builder)
        {
            double[] latencySeconds = new double[] { 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1.0, 2.5, 5.0, 10.0, 30.0, 60.0 };
            double[] tokenBuckets = new double[] { 16, 64, 256, 1024, 4096, 16384, 65536 };
            double[] byteBuckets = new double[] { 256, 1024, 4096, 16384, 65536, 262144, 1048576, 4194304 };

            string[] latencyInstruments = new string[]
            {
                "xeno.http.request.duration",
                "xeno.relay.roundtrip.duration",
                "xeno.relay.upstream.duration",
                "xeno.relay.time_to_first_token",
                "xeno.relay.streaming.duration"
            };

            foreach (string instrument in latencyInstruments)
            {
                builder.AddView(instrument, new ExplicitBucketHistogramConfiguration { Boundaries = latencySeconds });
            }

            builder.AddView("xeno.tokens.per_request", new ExplicitBucketHistogramConfiguration { Boundaries = tokenBuckets });
            builder.AddView("xeno.relay.request.size", new ExplicitBucketHistogramConfiguration { Boundaries = byteBuckets });
            builder.AddView("xeno.relay.response.size", new ExplicitBucketHistogramConfiguration { Boundaries = byteBuckets });
        }

        private void ConfigureOtlp(OtlpExporterOptions options)
        {
            OtlpExporterSettings otlp = _Settings.Otlp;

            if (!string.IsNullOrEmpty(otlp.Endpoint) && Uri.TryCreate(otlp.Endpoint, UriKind.Absolute, out Uri? endpoint))
                options.Endpoint = endpoint;

            options.Protocol = string.Equals(otlp.Protocol, "httpprotobuf", StringComparison.OrdinalIgnoreCase)
                ? OtlpExportProtocol.HttpProtobuf
                : OtlpExportProtocol.Grpc;

            options.TimeoutMilliseconds = otlp.TimeoutMs;

            if (!string.IsNullOrEmpty(otlp.Headers)) options.Headers = otlp.Headers;
        }

        private static string ResolveVersion()
        {
            Version? version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return version != null ? version.ToString() : "0.0.0";
        }

        /// <summary>
        /// Dispose managed resources.
        /// </summary>
        /// <param name="disposing">True when disposing.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed) return;

            if (disposing)
            {
                XenoTelemetry.ClearState();

                if (_MeterProvider != null)
                {
                    _MeterProvider.Dispose();
                    _MeterProvider = null;
                }

                if (_TracerProvider != null)
                {
                    _TracerProvider.Dispose();
                    _TracerProvider = null;
                }

                _Logging?.Debug(_Header + "telemetry disposed");
            }

            _Disposed = true;
        }

        #endregion
    }
}

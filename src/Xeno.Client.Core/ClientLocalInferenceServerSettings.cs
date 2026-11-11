namespace Xeno.Client
{
    using System.Collections.Generic;

    /// <summary>
    /// Local inference server listener settings.
    /// </summary>
    public class ClientLocalInferenceServerSettings
    {
        /// <summary>
        /// Default listener port. Ollama's default, 11434, is left free for a local Ollama.
        /// </summary>
        public const int DefaultPort = 11435;

        /// <summary>
        /// Listener port used by releases before 0.2.0. Settings that still hold it are migrated to <see cref="DefaultPort"/> once.
        /// </summary>
        public const int LegacyDefaultPort = 11434;

        private const int DefaultRequestTimeoutMs = 300000;
        private const int DefaultHealthCheckIntervalMs = 15000;
        private const int DefaultLocalQueueTimeoutMs = 30000;
        private int _Port = DefaultPort;
        private int _RequestTimeoutMs = DefaultRequestTimeoutMs;
        private int _MaxRequestBodyBytes = 10485760;
        private int _HealthCheckIntervalMs = DefaultHealthCheckIntervalMs;
        private int _LocalQueueTimeoutMs = DefaultLocalQueueTimeoutMs;
        private int _RequestRetentionDays = 30;
        private List<LocalInferenceRouteRule> _Routes = new List<LocalInferenceRouteRule>();

        /// <summary>
        /// Whether the local OpenAI-compatible and Ollama-compatible inference listener is enabled. Default is false.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Loopback listener host. Default is 127.0.0.1.
        /// </summary>
        public string Host { get; set; } = "127.0.0.1";

        /// <summary>
        /// Listener port. Default is 11435. Minimum 1, maximum 65535; out-of-range values reset to the default. Changing it requires restarting the listener.
        /// </summary>
        public int Port
        {
            get
            {
                return _Port;
            }
            set
            {
                _Port = value < 1 || value > 65535 ? DefaultPort : value;
            }
        }

        /// <summary>
        /// Whether CORS headers are emitted for localhost app integration. Default is true.
        /// </summary>
        public bool EnableCors { get; set; } = true;

        /// <summary>
        /// Allowed CORS origins for the loopback listener.
        /// </summary>
        public List<string> CorsAllowedOrigins { get; set; } = new List<string>
        {
            "http://localhost",
            "http://127.0.0.1",
            "http://[::1]"
        };

        /// <summary>
        /// Local inference request timeout in milliseconds. Default is 300000. Minimum 1000; smaller values reset to the default.
        /// </summary>
        public int RequestTimeoutMs
        {
            get
            {
                return _RequestTimeoutMs;
            }
            set
            {
                _RequestTimeoutMs = value < 1000 ? DefaultRequestTimeoutMs : value;
            }
        }

        /// <summary>
        /// Maximum request body bytes accepted by the local listener. Default is 10485760. Minimum 1024; smaller values reset to the default.
        /// </summary>
        public int MaxRequestBodyBytes
        {
            get
            {
                return _MaxRequestBodyBytes;
            }
            set
            {
                _MaxRequestBodyBytes = value < 1024 ? 10485760 : value;
            }
        }

        /// <summary>
        /// How requests choose between local providers and the Xeno network. Default is <see cref="LocalInferenceRoutingModeEnum.PreferLocal"/>. Takes effect without a restart.
        /// </summary>
        public LocalInferenceRoutingModeEnum RoutingMode { get; set; } = LocalInferenceRoutingModeEnum.PreferLocal;

        /// <summary>
        /// Per-model routing overrides. Empty by default, which routes local models locally and everything else to the network. Never null.
        /// </summary>
        public List<LocalInferenceRouteRule> Routes
        {
            get
            {
                return _Routes;
            }
            set
            {
                _Routes = value ?? new List<LocalInferenceRouteRule>();
            }
        }

        /// <summary>
        /// Whether models reported by local providers (for example a signed-in Ollama's cloud models) are routable even when they are not configured on the provider. Default is true.
        /// </summary>
        public bool DiscoverLocalModels { get; set; } = true;

        /// <summary>
        /// Interval between local provider health checks in milliseconds. Default is 15000. Clamped to 1000 through 300000.
        /// </summary>
        public int HealthCheckIntervalMs
        {
            get
            {
                return _HealthCheckIntervalMs;
            }
            set
            {
                _HealthCheckIntervalMs = Clamp(value, 1000, 300000);
            }
        }

        /// <summary>
        /// How long a local request waits for a free concurrency slot before it is rejected with 429, in milliseconds. Default is 30000. Clamped to 0 through 300000; 0 rejects immediately when the provider is full.
        /// </summary>
        public int LocalQueueTimeoutMs
        {
            get
            {
                return _LocalQueueTimeoutMs;
            }
            set
            {
                _LocalQueueTimeoutMs = Clamp(value, 0, 300000);
            }
        }

        /// <summary>
        /// Days local endpoint requests are kept for usage analytics in the local dashboard database. Default 30.
        /// Clamped to 1 through 365.
        /// </summary>
        public int RequestRetentionDays
        {
            get
            {
                return _RequestRetentionDays;
            }
            set
            {
                _RequestRetentionDays = Clamp(value, 1, 365);
            }
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum) return minimum;
            if (value > maximum) return maximum;
            return value;
        }
    }
}

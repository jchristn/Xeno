namespace Xeno.Client
{
    using System.Collections.Generic;

    /// <summary>
    /// Local inference server listener settings.
    /// </summary>
    public class ClientLocalInferenceServerSettings
    {
        private const int DefaultRequestTimeoutMs = 300000;
        private int _Port = 11434;
        private int _RequestTimeoutMs = DefaultRequestTimeoutMs;
        private int _MaxRequestBodyBytes = 10485760;

        /// <summary>
        /// Whether the local OpenAI-compatible and Ollama-compatible inference listener is enabled. Default is false.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Loopback listener host. Default is 127.0.0.1.
        /// </summary>
        public string Host { get; set; } = "127.0.0.1";

        /// <summary>
        /// Listener port. Default is 11434.
        /// </summary>
        public int Port
        {
            get
            {
                return _Port;
            }
            set
            {
                _Port = value < 1 || value > 65535 ? 11434 : value;
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
        /// Local inference request timeout in milliseconds. Default is 300000.
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
        /// Maximum request body bytes accepted by the local listener. Default is 10485760.
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
    }
}

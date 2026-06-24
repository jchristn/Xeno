namespace Xeno.Core.Settings
{
    using System;

    /// <summary>
    /// Root Xeno server settings.
    /// </summary>
    public class XenoSettings
    {
        /// <summary>
        /// Creation timestamp.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// REST settings.
        /// </summary>
        public RestSettings Rest { get; set; } = new RestSettings();

        /// <summary>
        /// CORS settings.
        /// </summary>
        public CorsSettings Cors { get; set; } = new CorsSettings();

        /// <summary>
        /// WebSocket settings.
        /// </summary>
        public WebSocketSettings WebSocket { get; set; } = new WebSocketSettings();

        /// <summary>
        /// Database settings.
        /// </summary>
        public DatabaseSettings Database { get; set; } = new DatabaseSettings();

        /// <summary>
        /// Logging settings.
        /// </summary>
        public LoggingSettings Logging { get; set; } = new LoggingSettings();

        /// <summary>
        /// Authentication settings.
        /// </summary>
        public AuthSettings Auth { get; set; } = new AuthSettings();

        /// <summary>
        /// Request history settings.
        /// </summary>
        public RequestHistorySettings RequestHistory { get; set; } = new RequestHistorySettings();

        /// <summary>
        /// Routing settings.
        /// </summary>
        public RoutingSettings Routing { get; set; } = new RoutingSettings();
    }
}

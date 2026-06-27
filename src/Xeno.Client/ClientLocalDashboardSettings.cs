namespace Xeno.Client
{
    /// <summary>
    /// Local dashboard settings for the Xeno client.
    /// </summary>
    public class ClientLocalDashboardSettings
    {
        /// <summary>
        /// Enable the local dashboard. Default is false.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Local dashboard HTTP host. Default is 127.0.0.1.
        /// </summary>
        public string Host { get; set; } = "127.0.0.1";

        /// <summary>
        /// Local dashboard HTTP port. Default is 9343.
        /// </summary>
        public int Port { get; set; } = 9343;

        /// <summary>
        /// Open the local dashboard in a browser after startup. Default is false.
        /// </summary>
        public bool OpenBrowserOnStart { get; set; } = false;

        /// <summary>
        /// Require a per-process local token for dashboard API calls. Default is true.
        /// </summary>
        public bool RequireLocalToken { get; set; } = true;

        /// <summary>
        /// Local dashboard SQLite database settings.
        /// </summary>
        public ClientLocalDatabaseSettings Database { get; set; } = new ClientLocalDatabaseSettings();

        /// <summary>
        /// Local request-history retention settings.
        /// </summary>
        public ClientLocalHistorySettings HistoryRetention { get; set; } = new ClientLocalHistorySettings();
    }
}

namespace Xeno.Tui
{
    using Xeno.Client.Presentation;

    /// <summary>
    /// Options for starting the Xeno terminal app.
    /// </summary>
    public sealed class TuiStartOptions
    {
        /// <summary>
        /// Page to open, or null for the last page used.
        /// </summary>
        public XenoPageEnum? StartPage { get; set; } = null;

        /// <summary>
        /// True to refresh status and pages on a timer. Tests set this to false for deterministic frames.
        /// </summary>
        public bool Live { get; set; } = true;

        /// <summary>
        /// True to connect this computer when the app starts, unless a worker is running, nobody is signed in, or
        /// XENO_AUTOCONNECT=0. Tests turn it off.
        /// </summary>
        public bool AutoConnect { get; set; } = true;

        /// <summary>
        /// Preferences file, or null for frontend.json in the client home directory.
        /// </summary>
        public string? PreferencesPath { get; set; } = null;

        /// <summary>
        /// Notification to show once the app starts, for example the server chosen with --server, or null.
        /// </summary>
        public string? StartupMessage { get; set; } = null;
    }
}

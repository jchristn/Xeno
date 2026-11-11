namespace Xeno.Client.Presentation
{
    using System;
    using Xeno.Client.Services;

    /// <summary>
    /// Decides which client commands are enabled for a status snapshot, and why a disabled command is unavailable.
    /// Shared so the desktop app, the terminal app, and the tray agent enable the same commands at the same time.
    /// </summary>
    public static class XenoCommandAvailability
    {
        /// <summary>
        /// Determine whether a command is enabled.
        /// </summary>
        /// <param name="command">Command.</param>
        /// <param name="snapshot">Current status, or null when unknown.</param>
        /// <returns>True when enabled.</returns>
        public static bool IsEnabled(XenoCommandEnum command, ClientStatusSnapshot? snapshot)
        {
            return DisabledReason(command, snapshot) == null;
        }

        /// <summary>
        /// Explain why a command is disabled.
        /// </summary>
        /// <param name="command">Command.</param>
        /// <param name="snapshot">Current status, or null when unknown.</param>
        /// <returns>Reason, or null when the command is enabled.</returns>
        public static string? DisabledReason(XenoCommandEnum command, ClientStatusSnapshot? snapshot)
        {
            switch (command)
            {
                case XenoCommandEnum.StartClient:
                    if (snapshot == null) return "Client status is loading.";
                    if (snapshot.Running) return "The client is already running.";
                    if (!snapshot.HasCredential) return "Sign in to a Xeno server first.";
                    return null;
                case XenoCommandEnum.StopClient:
                case XenoCommandEnum.RestartClient:
                    if (snapshot == null) return "Client status is loading.";
                    return snapshot.Running ? null : "The client is not running.";
                case XenoCommandEnum.OpenWebDashboard:
                    if (snapshot == null) return "Client status is loading.";
                    if (!snapshot.LocalDashboardEnabled) return "The local dashboard is disabled in Settings.";
                    return snapshot.CanOpenLocalDashboard ? null : "Start the client to open the local dashboard.";
                default:
                    return null;
            }
        }

        /// <summary>
        /// Status line text for menus and tooltips, for example "Xeno: Connected".
        /// </summary>
        /// <param name="snapshot">Current status, or null when unknown.</param>
        /// <returns>Status line.</returns>
        public static string StatusLine(ClientStatusSnapshot? snapshot)
        {
            return snapshot == null ? "Xeno: checking..." : "Xeno: " + snapshot.StatusText;
        }

        /// <summary>
        /// Status line for the tray agent: the status line plus the local endpoint address when the listener is enabled,
        /// for example "Xeno: Connected - local endpoint 127.0.0.1:11435".
        /// </summary>
        /// <param name="snapshot">Current status, or null when unknown.</param>
        /// <returns>Status line.</returns>
        public static string TrayStatusLine(ClientStatusSnapshot? snapshot)
        {
            string line = StatusLine(snapshot);
            if (snapshot == null || !snapshot.LocalInferenceEnabled) return line;
            string endpoint = snapshot.LocalInferenceUrl ?? String.Empty;
            int scheme = endpoint.IndexOf("://", StringComparison.Ordinal);
            if (scheme >= 0) endpoint = endpoint.Substring(scheme + 3);
            endpoint = endpoint.TrimEnd('/');
            if (endpoint.Length == 0) return line;
            if (!snapshot.LocalInferenceListening) return line + " - local endpoint " + endpoint + " (not running)";
            string health = snapshot.LocalInferenceRoutableBackends > 0
                ? ", " + snapshot.LocalInferenceHealthyBackends.ToString(System.Globalization.CultureInfo.InvariantCulture) + " of " + snapshot.LocalInferenceRoutableBackends.ToString(System.Globalization.CultureInfo.InvariantCulture) + " providers healthy"
                : String.Empty;
            return line + " - local endpoint " + endpoint + health;
        }

        /// <summary>
        /// Map a command to its appearance value, or null for other commands.
        /// </summary>
        /// <param name="command">Command.</param>
        /// <returns>Appearance, or null.</returns>
        public static XenoAppearanceEnum? AppearanceFor(XenoCommandEnum command)
        {
            switch (command)
            {
                case XenoCommandEnum.AppearanceSystem: return XenoAppearanceEnum.System;
                case XenoCommandEnum.AppearanceLight: return XenoAppearanceEnum.Light;
                case XenoCommandEnum.AppearanceDark: return XenoAppearanceEnum.Dark;
                default: return null;
            }
        }

        /// <summary>
        /// Seconds between chart refreshes for an auto-refresh command (0 for off), or null for other commands.
        /// </summary>
        /// <param name="command">Command.</param>
        /// <returns>Seconds, or null.</returns>
        public static int? AutoRefreshFor(XenoCommandEnum command)
        {
            switch (command)
            {
                case XenoCommandEnum.AutoRefreshOff: return 0;
                case XenoCommandEnum.AutoRefresh15: return 15;
                case XenoCommandEnum.AutoRefresh60: return 60;
                default: return null;
            }
        }

        /// <summary>
        /// External link opened by a help command, or null for other commands.
        /// </summary>
        /// <param name="command">Command.</param>
        /// <returns>URL, or null.</returns>
        public static string? LinkFor(XenoCommandEnum command)
        {
            switch (command)
            {
                case XenoCommandEnum.Documentation: return Hosting.XenoProduct.DocumentationUrl;
                case XenoCommandEnum.ReportIssue: return Hosting.XenoProduct.IssuesUrl;
                case XenoCommandEnum.Community: return Hosting.XenoProduct.CommunityUrl;
                default: return null;
            }
        }

        /// <summary>
        /// Short display name for a command, used by command palettes.
        /// </summary>
        /// <param name="command">Command.</param>
        /// <returns>Display name.</returns>
        public static string Title(XenoCommandEnum command)
        {
            XenoPageEnum? page = XenoNavigationCatalog.PageFor(command);
            if (page != null) return "Go to " + XenoNavigationCatalog.Get(page.Value).LongTitle;
            switch (command)
            {
                case XenoCommandEnum.NewProvider: return "Add Provider";
                case XenoCommandEnum.NewModel: return "Add Model";
                case XenoCommandEnum.OpenSettingsFile: return "Open client.json";
                case XenoCommandEnum.RevealDataFolder: return "Show Data Folder";
                case XenoCommandEnum.StartClient: return "Start Client";
                case XenoCommandEnum.StopClient: return "Stop Client";
                case XenoCommandEnum.RestartClient: return "Restart Client";
                case XenoCommandEnum.ReloadConfiguration: return "Re-advertise Models";
                case XenoCommandEnum.SignIn: return "Sign In";
                case XenoCommandEnum.TestConnection: return "Test Server Connection";
                case XenoCommandEnum.OpenWebDashboard: return "Open Web Dashboard";
                case XenoCommandEnum.OpenDesktopApp: return "Open Xeno Desktop";
                case XenoCommandEnum.OpenTerminalApp: return "Open Xeno Terminal";
                case XenoCommandEnum.AppearanceSystem: return "Appearance: System";
                case XenoCommandEnum.AppearanceLight: return "Appearance: Light";
                case XenoCommandEnum.AppearanceDark: return "Appearance: Dark";
                case XenoCommandEnum.AutoRefreshOff: return "Auto Refresh: Off";
                case XenoCommandEnum.AutoRefresh15: return "Auto Refresh: 15 Seconds";
                case XenoCommandEnum.AutoRefresh60: return "Auto Refresh: 1 Minute";
                case XenoCommandEnum.KeyboardShortcuts: return "Keyboard Shortcuts";
                case XenoCommandEnum.Documentation: return "Xeno Documentation";
                case XenoCommandEnum.ReportIssue: return "Report an Issue";
                case XenoCommandEnum.Community: return "Xeno Community";
                case XenoCommandEnum.CommandPalette: return "Command Palette";
                default: return command.ToString();
            }
        }
    }
}

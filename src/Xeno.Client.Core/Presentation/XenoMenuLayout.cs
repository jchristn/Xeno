namespace Xeno.Client.Presentation
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// The one menu definition shared by the desktop app (native macOS menu bar, in-window menu elsewhere), the
    /// terminal app (menu bar), and the tray agent (tray menu and macOS application menu).
    /// </summary>
    public static class XenoMenuLayout
    {
        #region Public-Methods

        /// <summary>
        /// The macOS application menu items placed before the system-provided Services, Hide, and Quit items.
        /// </summary>
        /// <param name="surface">Front end.</param>
        /// <returns>Entries.</returns>
        public static List<XenoMenuEntry> ApplicationMenu(XenoSurfaceEnum surface)
        {
            List<XenoMenuEntry> entries = new List<XenoMenuEntry>
            {
                XenoMenuEntry.Item(surface == XenoSurfaceEnum.Tray ? "About Xeno Agent" : "About Xeno", XenoCommandEnum.About)
            };

            if (surface == XenoSurfaceEnum.Desktop)
            {
                entries.Add(XenoMenuEntry.Separator());
                entries.Add(XenoMenuEntry.Item("Settings...", XenoCommandEnum.Settings, ","));
            }

            if (surface == XenoSurfaceEnum.Tray)
            {
                entries.Add(XenoMenuEntry.Separator());
                entries.Add(XenoMenuEntry.Item("Open Xeno", XenoCommandEnum.OpenDesktopApp));
                entries.Add(XenoMenuEntry.Item("Open Xeno Terminal", XenoCommandEnum.OpenTerminalApp));
                entries.Add(XenoMenuEntry.Item("Open Web Dashboard", XenoCommandEnum.OpenWebDashboard));
                entries.Add(XenoMenuEntry.Separator());
                entries.Add(XenoMenuEntry.Item("Settings...", XenoCommandEnum.Settings, ","));
            }

            return entries;
        }

        /// <summary>
        /// The menu bar: File, Edit (macOS desktop only), View, Client, Window (macOS desktop only), and Help.
        /// </summary>
        /// <param name="surface">Front end (desktop or terminal).</param>
        /// <param name="isMacOS">True when building the native macOS menu bar.</param>
        /// <returns>Top-level submenus.</returns>
        public static List<XenoMenuEntry> MenuBar(XenoSurfaceEnum surface, bool isMacOS)
        {
            bool desktop = surface == XenoSurfaceEnum.Desktop;
            bool macDesktop = desktop && isMacOS;
            List<XenoMenuEntry> menus = new List<XenoMenuEntry>();

            menus.Add(XenoMenuEntry.Submenu("File",
                XenoMenuEntry.Item("Add Provider...", XenoCommandEnum.NewProvider, "N"),
                XenoMenuEntry.Item("Add Model...", XenoCommandEnum.NewModel, "N", true),
                XenoMenuEntry.Item("Import Models...", XenoCommandEnum.ImportModels),
                XenoMenuEntry.Separator(),
                XenoMenuEntry.Item("Open client.json", XenoCommandEnum.OpenSettingsFile),
                XenoMenuEntry.Item(isMacOS ? "Show Data Folder in Finder" : "Show Data Folder", XenoCommandEnum.RevealDataFolder),
                macDesktop ? null : XenoMenuEntry.Separator(),
                macDesktop ? null : XenoMenuEntry.Item("Settings...", XenoCommandEnum.Settings, ","),
                macDesktop ? XenoMenuEntry.Separator() : null,
                macDesktop ? XenoMenuEntry.Item("Close Window", XenoCommandEnum.CloseWindow, "W") : null,
                macDesktop ? null : XenoMenuEntry.Separator(),
                macDesktop ? null : XenoMenuEntry.Item(desktop ? "Exit" : "Quit", XenoCommandEnum.Quit, "Q")));

            if (macDesktop)
            {
                menus.Add(XenoMenuEntry.Submenu("Edit",
                    XenoMenuEntry.Item("Undo", XenoCommandEnum.Undo, "Z"),
                    XenoMenuEntry.Item("Redo", XenoCommandEnum.Redo, "Z", true),
                    XenoMenuEntry.Separator(),
                    XenoMenuEntry.Item("Cut", XenoCommandEnum.Cut, "X"),
                    XenoMenuEntry.Item("Copy", XenoCommandEnum.Copy, "C"),
                    XenoMenuEntry.Item("Paste", XenoCommandEnum.Paste, "V"),
                    XenoMenuEntry.Item("Select All", XenoCommandEnum.SelectAll, "A")));
            }

            List<XenoMenuEntry?> view = new List<XenoMenuEntry?>();
            foreach (XenoPageDefinition page in XenoNavigationCatalog.Pages)
            {
                view.Add(page.ShortcutDigit < 0 ? XenoMenuEntry.Item(page.LongTitle, page.Command) : XenoMenuEntry.Item(page.LongTitle, page.Command, page.ShortcutDigit.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            }

            view.Add(XenoMenuEntry.Separator());
            view.Add(XenoMenuEntry.Item("Refresh", XenoCommandEnum.Refresh, desktop ? "R" : "F5"));
            view.Add(XenoMenuEntry.Item("Command Palette...", XenoCommandEnum.CommandPalette, "K"));
            view.Add(XenoMenuEntry.Submenu("Auto Refresh",
                XenoMenuEntry.Radio("Off", XenoCommandEnum.AutoRefreshOff),
                XenoMenuEntry.Radio("15 Seconds", XenoCommandEnum.AutoRefresh15),
                XenoMenuEntry.Radio("1 Minute", XenoCommandEnum.AutoRefresh60)));
            view.Add(XenoMenuEntry.Submenu("Appearance",
                XenoMenuEntry.Radio("System", XenoCommandEnum.AppearanceSystem),
                XenoMenuEntry.Radio("Light", XenoCommandEnum.AppearanceLight),
                XenoMenuEntry.Radio("Dark", XenoCommandEnum.AppearanceDark)));
            menus.Add(XenoMenuEntry.Submenu("View", view.ToArray()));

            menus.Add(XenoMenuEntry.Submenu("Client",
                XenoMenuEntry.Item("Start Client", XenoCommandEnum.StartClient),
                XenoMenuEntry.Item("Stop Client", XenoCommandEnum.StopClient),
                XenoMenuEntry.Item("Restart Client", XenoCommandEnum.RestartClient),
                XenoMenuEntry.Separator(),
                XenoMenuEntry.Item("Re-advertise Models", XenoCommandEnum.ReloadConfiguration),
                XenoMenuEntry.Item("Sign In...", XenoCommandEnum.SignIn),
                XenoMenuEntry.Item("Test Server Connection", XenoCommandEnum.TestConnection),
                XenoMenuEntry.Separator(),
                XenoMenuEntry.Item("Open Web Dashboard", XenoCommandEnum.OpenWebDashboard, "D", true),
                desktop ? XenoMenuEntry.Item("Open Xeno Terminal", XenoCommandEnum.OpenTerminalApp, "T", true) : XenoMenuEntry.Item("Open Xeno Desktop", XenoCommandEnum.OpenDesktopApp)));

            if (macDesktop)
            {
                menus.Add(XenoMenuEntry.Submenu("Window",
                    XenoMenuEntry.Item("Minimize", XenoCommandEnum.MinimizeWindow, "M"),
                    XenoMenuEntry.Item("Zoom", XenoCommandEnum.ZoomWindow),
                    XenoMenuEntry.Separator(),
                    XenoMenuEntry.Item("Bring All to Front", XenoCommandEnum.BringAllToFront)));
            }

            menus.Add(XenoMenuEntry.Submenu("Help",
                XenoMenuEntry.Item("Keyboard Shortcuts", XenoCommandEnum.KeyboardShortcuts, desktop ? "/" : "F1"),
                XenoMenuEntry.Separator(),
                XenoMenuEntry.Item("Xeno Documentation", XenoCommandEnum.Documentation),
                XenoMenuEntry.Item("Report an Issue...", XenoCommandEnum.ReportIssue),
                XenoMenuEntry.Item("Xeno Community (Discord)", XenoCommandEnum.Community),
                macDesktop ? null : XenoMenuEntry.Separator(),
                macDesktop ? null : XenoMenuEntry.Item("About Xeno", XenoCommandEnum.About)));

            return menus;
        }

        /// <summary>
        /// The tray menu: status line, launchers for every front end, client control, and quit.
        /// </summary>
        /// <returns>Entries.</returns>
        public static List<XenoMenuEntry> TrayMenu()
        {
            return new List<XenoMenuEntry>
            {
                XenoMenuEntry.StatusLine(),
                XenoMenuEntry.Separator(),
                XenoMenuEntry.Item("Open Xeno Desktop", XenoCommandEnum.OpenDesktopApp),
                XenoMenuEntry.Item("Open Xeno Terminal", XenoCommandEnum.OpenTerminalApp),
                XenoMenuEntry.Item("Open Web Dashboard", XenoCommandEnum.OpenWebDashboard),
                XenoMenuEntry.Item("Settings", XenoCommandEnum.Settings),
                XenoMenuEntry.Separator(),
                XenoMenuEntry.Item("Start Client", XenoCommandEnum.StartClient),
                XenoMenuEntry.Item("Stop Client", XenoCommandEnum.StopClient),
                XenoMenuEntry.Item("Restart Client", XenoCommandEnum.RestartClient),
                XenoMenuEntry.Separator(),
                XenoMenuEntry.Item("About Xeno Agent", XenoCommandEnum.About),
                XenoMenuEntry.Item("Quit Xeno", XenoCommandEnum.Quit)
            };
        }

        /// <summary>
        /// Every command reachable from a set of menus.
        /// </summary>
        /// <param name="menus">Menus.</param>
        /// <returns>Distinct commands.</returns>
        public static List<XenoCommandEnum> Commands(IEnumerable<XenoMenuEntry> menus)
        {
            return menus
                .SelectMany(item => item.Flatten())
                .Where(item => item.Kind == XenoMenuEntryKindEnum.Item || item.Kind == XenoMenuEntryKindEnum.Radio)
                .Select(item => item.Command)
                .Distinct()
                .ToList();
        }

        #endregion
    }
}

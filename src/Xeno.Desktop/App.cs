namespace Xeno.Desktop
{
    using System;
    using System.Threading;
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Controls.ApplicationLifetimes;
    using Avalonia.Input;
    using Avalonia.Threading;
    using Avalonia.Themes.Fluent;
    using Xeno.Client;
    using Xeno.Client.Hosting;
    using Xeno.Client.Presentation;
    using Xeno.Desktop.Common;
    using Xeno.Desktop.Dialogs;

    /// <summary>
    /// The Xeno desktop application. Owns the session, the main window, and the native menus, and executes every menu
    /// command. On macOS the application name "Xeno" appears in the menu bar, with an application menu (About, Settings)
    /// and a window menu bar (File, Edit, View, Client, Window, Help). Elsewhere the same menus render inside the window.
    /// </summary>
    public sealed class App : Application, IXenoMenuHost
    {
        #region Public-Members

        /// <summary>
        /// Status line for menus.
        /// </summary>
        public string StatusLine
        {
            get { return XenoCommandAvailability.StatusLine(_Session?.Status); }
        }

        #endregion

        #region Private-Members

        private DesktopSession? _Session;
        private MainWindow? _Main;
        private XenoNativeMenuBuilder? _Menus;
        private SettingsWindow? _Settings;
        private DispatcherTimer? _ActivationTimer;
        private readonly DateTime _StartedUtc = DateTime.UtcNow;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Initialize the application: name, theme, and the macOS application menu.
        /// </summary>
        public override void Initialize()
        {
            // Without a name, macOS shows "Avalonia Application" in the menu bar and the app switcher.
            Name = XenoProduct.DesktopName;
            Styles.Add(new FluentTheme());
            _Menus = new XenoNativeMenuBuilder(this);

            // The application menu must be installed before Avalonia creates its default "About Avalonia" menu.
            if (OperatingSystem.IsMacOS())
            {
                NativeMenu.SetMenu(this, _Menus.Build(XenoMenuLayout.ApplicationMenu(XenoSurfaceEnum.Desktop), true));
            }
        }

        /// <summary>
        /// Create the session and main window.
        /// </summary>
        public override void OnFrameworkInitializationCompleted()
        {
            // Like the tray-first pattern in Mux, the desktop app brings up the Xeno Agent so the client stays reachable
            // from the menu bar after this window closes. Opt out with XENO_AGENT_AUTOSTART=0.
            XenoAppLauncher.EnsureTrayRunning();
            _Session = new DesktopSession();
            XenoErrorLog.Install("xeno-desktop", message => _Session?.Report(message));
            XenoTheme.Apply(this, _Session.Preferences.DesktopAppearance, XenoColorSchemes.Selected(_Session.Preferences));
            if (OperatingSystem.IsMacOS() && !MacPlatform.IsRunningFromBundle())
            {
                using (System.IO.Stream? logo = XenoAssets.OpenLogoPng())
                {
                    MacPlatform.TryApplyDockIcon(logo);
                }
            }

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                XenoPageEnum start = Program.StartPage ?? _Session.Preferences.DesktopLastPage;
                _Main = new MainWindow(_Session, this, start);
                desktop.MainWindow = _Main;
                desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
                desktop.ShutdownRequested += (sender, args) => _Session.Dispose();
                Window.WindowOpenedEvent.AddClassHandler<Window>((window, args) => AttachWindowMenu(window));
                _Session.Changed += (sender, args) => _Menus?.Refresh();
                if (!String.IsNullOrWhiteSpace(Program.StartupMessage)) _Session.Report(Program.StartupMessage);
                AutoConnect(_Session);

                // Another process (the tray agent, or a second launch) asks this instance to come forward by posting a
                // request under the client home. A request left over from before this launch is dropped.
                DesktopActivation.Clear();
                _ActivationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
                _ActivationTimer.Tick += (sender, args) => TakeActivationRequest();
                _ActivationTimer.Start();
                desktop.ShutdownRequested += (sender, args) => _ActivationTimer?.Stop();
                if (Program.OpenSettingsAtStart) _Main.Opened += (sender, args) => ShowSettings();
            }

            base.OnFrameworkInitializationCompleted();
        }

        private static void AutoConnect(DesktopSession session)
        {
            // Opening the app connects this computer, as the terminal app and the tray agent do. XENO_AUTOCONNECT=0 opts out.
            XenoAutoConnectDecisionEnum decision = XenoAutoConnect.Decide(session.Workspace);
            if (decision == XenoAutoConnectDecisionEnum.Connect)
            {
                _ = session.RunBusyAsync(XenoAutoConnect.ProgressMessage, () => XenoAutoConnect.ConnectAsync(session.Workspace, CancellationToken.None));
            }
            else if (decision == XenoAutoConnectDecisionEnum.NotSignedIn)
            {
                session.Report(XenoAutoConnect.NotSignedInMessage);
            }
        }

        /// <summary>
        /// Determine whether a command is enabled.
        /// </summary>
        /// <param name="command">Command.</param>
        /// <returns>True when enabled.</returns>
        public bool CanExecute(XenoCommandEnum command)
        {
            if (_Session != null && _Session.Busy && (command == XenoCommandEnum.StartClient || command == XenoCommandEnum.StopClient || command == XenoCommandEnum.RestartClient))
            {
                return false;
            }

            return XenoCommandAvailability.IsEnabled(command, _Session?.Status);
        }

        /// <summary>
        /// Determine whether a radio command is selected.
        /// </summary>
        /// <param name="command">Command.</param>
        /// <returns>True when selected.</returns>
        public bool IsChecked(XenoCommandEnum command)
        {
            int? autoRefresh = XenoCommandAvailability.AutoRefreshFor(command);
            if (autoRefresh != null) return _Session != null && autoRefresh == _Session.Preferences.AutoRefreshSeconds;
            XenoAppearanceEnum? appearance = XenoCommandAvailability.AppearanceFor(command);
            return appearance != null && appearance == XenoTheme.Appearance;
        }

        /// <summary>
        /// Execute a command.
        /// </summary>
        /// <param name="command">Command.</param>
        public void Execute(XenoCommandEnum command)
        {
            DesktopSession? session = _Session;
            MainWindow? main = _Main;
            if (session == null || main == null) return;

            XenoPageEnum? page = XenoNavigationCatalog.PageFor(command);
            if (page != null)
            {
                main.Navigate(page.Value);
                return;
            }

            XenoAppearanceEnum? appearance = XenoCommandAvailability.AppearanceFor(command);
            if (appearance != null)
            {
                session.Preferences.DesktopAppearance = appearance.Value;
                session.SavePreferences();
                XenoTheme.Apply(this, appearance.Value);
                _Menus?.Refresh();
                return;
            }

            int? autoRefresh = XenoCommandAvailability.AutoRefreshFor(command);
            if (autoRefresh != null)
            {
                session.Preferences.AutoRefreshSeconds = autoRefresh.Value;
                session.SavePreferences();
                _Menus?.Refresh();
                return;
            }

            string? link = XenoCommandAvailability.LinkFor(command);
            if (link != null)
            {
                session.Report(XenoAppLauncher.OpenUrl(link).Message);
                return;
            }

            switch (command)
            {
                case XenoCommandEnum.About:
                    _ = XenoDialogs.ShowModalAsync<object>(new AboutWindow(XenoProduct.DesktopName), main);
                    break;
                case XenoCommandEnum.Settings:
                    ShowSettings();
                    break;
                case XenoCommandEnum.Quit:
                    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime) lifetime.Shutdown();
                    break;
                case XenoCommandEnum.CloseWindow:
                    ActiveWindow()?.Close();
                    break;
                case XenoCommandEnum.Refresh:
                    main.RefreshCurrent();
                    break;
                case XenoCommandEnum.NewProvider:
                    _ = ClientDialogs.AddProviderAsync(main, session, main.RefreshCurrent);
                    break;
                case XenoCommandEnum.NewModel:
                    _ = ClientDialogs.AddModelAsync(main, session, null, main.RefreshCurrent);
                    break;
                case XenoCommandEnum.OpenSettingsFile:
                    session.Report(XenoAppLauncher.OpenUrl(ClientSettings.SettingsFile).Message);
                    break;
                case XenoCommandEnum.RevealDataFolder:
                    session.Report(XenoAppLauncher.OpenUrl(ClientSettings.SettingsDirectory).Message);
                    break;
                case XenoCommandEnum.StartClient:
                    _ = session.RunBusyAsync("Starting the client...", () => XenoClientActions.StartAsync(session.Workspace, CancellationToken.None));
                    break;
                case XenoCommandEnum.StopClient:
                    _ = session.RunBusyAsync("Stopping the client...", () => XenoClientActions.StopAsync(session.Workspace, CancellationToken.None));
                    break;
                case XenoCommandEnum.RestartClient:
                    _ = session.RunBusyAsync("Restarting the client...", () => XenoClientActions.RestartAsync(session.Workspace, CancellationToken.None));
                    break;
                case XenoCommandEnum.ReloadConfiguration:
                    session.Report(XenoClientActions.RequestInventoryUpdate(session.Workspace));
                    break;
                case XenoCommandEnum.SignIn:
                    _ = ClientDialogs.SignInAsync(main, session, main.RefreshCurrent, Execute);
                    break;
                case XenoCommandEnum.TestConnection:
                    _ = TestConnectionAsync(session);
                    break;
                case XenoCommandEnum.OpenWebDashboard:
                    session.Report(XenoClientActions.OpenWebDashboard());
                    break;
                case XenoCommandEnum.OpenTerminalApp:
                    session.Report(XenoClientActions.OpenTerminalApp());
                    break;
                case XenoCommandEnum.OpenDesktopApp:
                    main.Activate();
                    break;
                case XenoCommandEnum.Undo:
                case XenoCommandEnum.Redo:
                case XenoCommandEnum.Cut:
                case XenoCommandEnum.Copy:
                case XenoCommandEnum.Paste:
                case XenoCommandEnum.SelectAll:
                    ForwardEdit(command);
                    break;
                case XenoCommandEnum.MinimizeWindow:
                    Window? minimize = ActiveWindow();
                    if (minimize != null) minimize.WindowState = WindowState.Minimized;
                    break;
                case XenoCommandEnum.ZoomWindow:
                    Window? zoom = ActiveWindow();
                    if (zoom != null) zoom.WindowState = zoom.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                    break;
                case XenoCommandEnum.BringAllToFront:
                    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime all)
                    {
                        foreach (Window window in all.Windows) window.Activate();
                    }

                    break;
                case XenoCommandEnum.KeyboardShortcuts:
                    _ = XenoDialogs.ShowModalAsync<object>(new ShortcutsWindow(), main);
                    break;
                case XenoCommandEnum.CommandPalette:
                    _ = CommandPaletteWindow.ShowAsync(main, this);
                    break;
            }
        }

        #endregion

        #region Private-Methods

        /// <summary>
        /// Open the Settings window, or bring it to the front when it is already open.
        /// </summary>
        private void ShowSettings()
        {
            if (_Session == null) return;
            if (_Settings != null)
            {
                if (_Settings.WindowState == WindowState.Minimized) _Settings.WindowState = WindowState.Normal;
                _Settings.Activate();
                return;
            }

            _Settings = new SettingsWindow(_Session);
            _Settings.Closed += (sender, args) => _Settings = null;
            _Settings.Show();
            _Settings.Activate();
        }

        private void TakeActivationRequest()
        {
            if (XenoQuitBroadcast.IsRequestedSince(_StartedUtc) && ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
            {
                // Quitting the Xeno Agent quits every Xeno app.
                _ActivationTimer?.Stop();
                lifetime.Shutdown();
                return;
            }

            DesktopActivationRequest? request = DesktopActivation.Take();
            if (request == null || _Main == null) return;
            if (!_Main.IsVisible) _Main.Show();
            if (_Main.WindowState == WindowState.Minimized) _Main.WindowState = WindowState.Normal;
            if (request.Page.HasValue) _Main.Navigate(request.Page.Value);
            _Main.Activate();
            if (request.OpenSettings) ShowSettings();
        }

        private void AttachWindowMenu(Window window)
        {
            if (_Menus == null) return;
            NativeMenu menu = _Menus.Build(XenoMenuLayout.MenuBar(XenoSurfaceEnum.Desktop, OperatingSystem.IsMacOS()), true);
            NativeMenu.SetMenu(window, menu);
            window.Closed += (sender, args) => _Menus.Release(menu);
        }

        private System.Threading.Tasks.Task TestConnectionAsync(DesktopSession session)
        {
            return Dialogs.ClientDialogs.TestConnectionAsync(ActiveWindow(), session);
        }

        private Window? ActiveWindow()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                foreach (Window window in desktop.Windows)
                {
                    if (window.IsActive) return window;
                }
            }

            return _Main;
        }

        private void ForwardEdit(XenoCommandEnum command)
        {
            Window? window = ActiveWindow();
            if (window == null) return;
            IInputElement? focused = window.FocusManager?.GetFocusedElement();
            if (focused is TextBox box)
            {
                switch (command)
                {
                    case XenoCommandEnum.Undo: box.Undo(); break;
                    case XenoCommandEnum.Redo: box.Redo(); break;
                    case XenoCommandEnum.Cut: box.Cut(); break;
                    case XenoCommandEnum.Copy: box.Copy(); break;
                    case XenoCommandEnum.Paste: box.Paste(); break;
                    case XenoCommandEnum.SelectAll: box.SelectAll(); break;
                }

                return;
            }

            if (focused is SelectableTextBlock block)
            {
                if (command == XenoCommandEnum.Copy) block.Copy();
                if (command == XenoCommandEnum.SelectAll) block.SelectAll();
            }
        }

        #endregion
    }
}

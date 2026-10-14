namespace Xeno.Tray
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Controls.ApplicationLifetimes;
    using Avalonia.Themes.Fluent;
    using Avalonia.Threading;
    using Xeno.Client.Hosting;
    using Xeno.Client.Presentation;
    using Xeno.Client.Services;
    using Xeno.Desktop.Common;

    /// <summary>
    /// The Xeno Agent tray application. Shows the client status, starts and stops the background worker, and launches
    /// the desktop app, the terminal app, and the local web dashboard. Its menus come from the shared
    /// <see cref="XenoMenuLayout"/>; on macOS the application is named "Xeno Agent" with an application menu.
    /// </summary>
    public sealed class App : Application, IXenoMenuHost
    {
        #region Public-Members

        /// <summary>
        /// Status line for the tray menu.
        /// </summary>
        public string StatusLine
        {
            get { return XenoCommandAvailability.StatusLine(_Status); }
        }

        #endregion

        #region Private-Members

        private readonly XenoWorkspace _Workspace = new XenoWorkspace();
        private XenoNativeMenuBuilder? _Menus;
        private TrayIcon? _Tray;
        private DispatcherTimer? _Timer;
        private ClientStatusSnapshot? _Status;
        private bool _Busy = false;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Initialize the application: name, theme, and the macOS application menu.
        /// </summary>
        public override void Initialize()
        {
            Name = XenoProduct.TrayName;
            Styles.Add(new FluentTheme());
            _Menus = new XenoNativeMenuBuilder(this);
            if (OperatingSystem.IsMacOS())
            {
                NativeMenu.SetMenu(this, _Menus.Build(XenoMenuLayout.ApplicationMenu(XenoSurfaceEnum.Tray), false));
            }
        }

        /// <summary>
        /// Install the tray icon and start refreshing status.
        /// </summary>
        public override void OnFrameworkInitializationCompleted()
        {
            XenoTheme.Apply(this, XenoFrontEndPreferences.Load().DesktopAppearance);
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                desktop.ShutdownRequested += (sender, args) => _Workspace.Dispose();
            }

            InstallTray();
            _Timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _Timer.Tick += (sender, args) => _ = RefreshAsync();
            _Timer.Start();
            _ = RefreshAsync();
            base.OnFrameworkInitializationCompleted();
        }

        /// <summary>
        /// Determine whether a command is enabled.
        /// </summary>
        /// <param name="command">Command.</param>
        /// <returns>True when enabled.</returns>
        public bool CanExecute(XenoCommandEnum command)
        {
            if (_Busy && (command == XenoCommandEnum.StartClient || command == XenoCommandEnum.StopClient || command == XenoCommandEnum.RestartClient)) return false;
            return XenoCommandAvailability.IsEnabled(command, _Status);
        }

        /// <summary>
        /// The tray has no radio items.
        /// </summary>
        /// <param name="command">Command.</param>
        /// <returns>False.</returns>
        public bool IsChecked(XenoCommandEnum command)
        {
            return false;
        }

        /// <summary>
        /// Execute a command.
        /// </summary>
        /// <param name="command">Command.</param>
        public void Execute(XenoCommandEnum command)
        {
            switch (command)
            {
                case XenoCommandEnum.OpenDesktopApp:
                case XenoCommandEnum.ShowWindow:
                    _ = ReportAsync(XenoAppLauncher.LaunchDesktop());
                    break;
                case XenoCommandEnum.OpenTerminalApp:
                    _ = ReportAsync(XenoAppLauncher.LaunchTerminalApp());
                    break;
                case XenoCommandEnum.OpenWebDashboard:
                    _ = ReportAsync(XenoAppLauncher.OpenLocalDashboard());
                    break;
                case XenoCommandEnum.StartClient:
                    _ = RunAsync(token => XenoClientActions.StartAsync(_Workspace, token));
                    break;
                case XenoCommandEnum.StopClient:
                    _ = RunAsync(token => XenoClientActions.StopAsync(_Workspace, token));
                    break;
                case XenoCommandEnum.RestartClient:
                    _ = RunAsync(token => XenoClientActions.RestartAsync(_Workspace, token));
                    break;
                case XenoCommandEnum.About:
                    _ = XenoDialogs.ShowModalAsync<object>(new AboutWindow(XenoProduct.TrayName), null);
                    break;
                case XenoCommandEnum.Quit:
                    Quit();
                    break;
            }
        }

        #endregion

        #region Private-Methods

        private void InstallTray()
        {
            if (_Menus == null) return;
            _Tray = new TrayIcon
            {
                Icon = XenoAssets.LoadWindowIcon(),
                ToolTipText = XenoProduct.TrayName,
                Menu = _Menus.Build(XenoMenuLayout.TrayMenu(), false),
                IsVisible = true
            };
            _Tray.Clicked += (sender, args) =>
            {
                // On Windows and Linux a click opens the desktop app; on macOS a click opens the menu.
                if (!OperatingSystem.IsMacOS()) Execute(XenoCommandEnum.OpenDesktopApp);
            };

            // A tray icon that is only constructed never appears; it must be registered with the application.
            TrayIcon.SetIcons(this, new TrayIcons { _Tray });
        }

        private async Task RefreshAsync()
        {
            _Status = await Task.Run(() => _Workspace.GetStatus()).ConfigureAwait(true);
            if (_Tray != null) _Tray.ToolTipText = XenoProduct.TrayName + " - " + _Status.StatusText;
            _Menus?.Refresh();
        }

        private async Task RunAsync(Func<CancellationToken, Task<string>> operation)
        {
            if (_Busy) return;
            _Busy = true;
            _Menus?.Refresh();
            string message;
            try
            {
                message = await operation(CancellationToken.None).ConfigureAwait(true);
            }
            finally
            {
                _Busy = false;
            }

            await RefreshAsync().ConfigureAwait(true);
            if (_Tray != null) _Tray.ToolTipText = XenoProduct.TrayName + " - " + message;
            if (message.StartsWith("Could not", StringComparison.Ordinal) || message.Contains("did not", StringComparison.Ordinal))
            {
                await XenoDialogs.MessageAsync(null, XenoProduct.TrayName, message, XenoToneEnum.Warning).ConfigureAwait(true);
            }
        }

        private async Task ReportAsync(LaunchResult result)
        {
            if (!result.Success)
            {
                await XenoDialogs.MessageAsync(null, XenoProduct.TrayName, result.Message, XenoToneEnum.Warning).ConfigureAwait(true);
            }
        }

        private void Quit()
        {
            _Timer?.Stop();
            if (_Tray != null) _Tray.IsVisible = false;
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) desktop.Shutdown();
        }

        #endregion
    }
}

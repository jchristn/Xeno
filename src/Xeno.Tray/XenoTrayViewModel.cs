namespace Xeno.Tray
{
    using System;
    using System.ComponentModel;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Input;
    using Avalonia;
    using Avalonia.Controls.ApplicationLifetimes;
    using Avalonia.Threading;

    /// <summary>
    /// Tray view model and command coordinator.
    /// </summary>
    public sealed class XenoTrayViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly IXenoTrayClientController _Controller;
        private readonly DispatcherTimer _RefreshTimer;
        private StatisticsWindow? _StatisticsWindow;
        private string _StatusHeader = "Status: Unknown";
        private string _ToolTip = "Xeno Client";
        private string _LastMessage = string.Empty;
        private bool _StartEnabled = true;
        private bool _OpenDashboardEnabled = false;
        private bool _ExitClientEnabled = false;
        private bool _Disposed = false;
        private XenoTrayStatus _Status = new XenoTrayStatus();

        /// <summary>
        /// Raised when a property changes.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Instantiate the view model.
        /// </summary>
        /// <param name="controller">Client controller.</param>
        public XenoTrayViewModel(IXenoTrayClientController controller)
        {
            _Controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _RefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _RefreshTimer.Tick += async delegate
            {
                await RefreshAsync(CancellationToken.None).ConfigureAwait(false);
            };
            StartCommand = new RelayCommand(delegate { RunAsync(StartClientAsync); });
            ExitClientCommand = new RelayCommand(delegate { RunAsync(ExitClientAsync); });
            ShowStatisticsCommand = new RelayCommand(delegate { RunAsync(ShowStatisticsAsync); });
            OpenDashboardCommand = new RelayCommand(delegate { RunAsync(OpenDashboardAsync); });
            QuitTrayCommand = new RelayCommand(delegate { QuitTray(); });
        }

        /// <summary>
        /// Status header shown in the tray menu.
        /// </summary>
        public string StatusHeader
        {
            get
            {
                return _StatusHeader;
            }
            private set
            {
                SetField(ref _StatusHeader, value);
            }
        }

        /// <summary>
        /// Tray tooltip text.
        /// </summary>
        public string ToolTip
        {
            get
            {
                return _ToolTip;
            }
            private set
            {
                SetField(ref _ToolTip, value);
            }
        }

        /// <summary>
        /// Last operation message.
        /// </summary>
        public string LastMessage
        {
            get
            {
                return _LastMessage;
            }
            private set
            {
                SetField(ref _LastMessage, value);
            }
        }

        /// <summary>
        /// Whether the start command should be enabled.
        /// </summary>
        public bool StartEnabled
        {
            get
            {
                return _StartEnabled;
            }
            private set
            {
                SetField(ref _StartEnabled, value);
            }
        }

        /// <summary>
        /// Whether the open dashboard command should be enabled.
        /// </summary>
        public bool OpenDashboardEnabled
        {
            get
            {
                return _OpenDashboardEnabled;
            }
            private set
            {
                SetField(ref _OpenDashboardEnabled, value);
            }
        }

        /// <summary>
        /// Whether the exit client command should be enabled.
        /// </summary>
        public bool ExitClientEnabled
        {
            get
            {
                return _ExitClientEnabled;
            }
            private set
            {
                SetField(ref _ExitClientEnabled, value);
            }
        }

        /// <summary>
        /// Start command.
        /// </summary>
        public ICommand StartCommand { get; }

        /// <summary>
        /// Exit client command.
        /// </summary>
        public ICommand ExitClientCommand { get; }

        /// <summary>
        /// Show statistics command.
        /// </summary>
        public ICommand ShowStatisticsCommand { get; }

        /// <summary>
        /// Open dashboard command.
        /// </summary>
        public ICommand OpenDashboardCommand { get; }

        /// <summary>
        /// Quit tray command.
        /// </summary>
        public ICommand QuitTrayCommand { get; }

        /// <summary>
        /// Start periodic refresh.
        /// </summary>
        public void Start()
        {
            RunAsync(RefreshOnlyAsync);
            _RefreshTimer.Start();
        }

        /// <summary>
        /// Dispose the view model.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;
            _RefreshTimer.Stop();
            _StatisticsWindow?.Close();
        }

        private async Task RefreshOnlyAsync()
        {
            await RefreshAsync(CancellationToken.None).ConfigureAwait(false);
        }

        private async Task StartClientAsync()
        {
            XenoTrayOperationResult result = await _Controller.StartAsync(CancellationToken.None).ConfigureAwait(false);
            LastMessage = result.Message;
            await RefreshAsync(CancellationToken.None).ConfigureAwait(false);
        }

        private async Task ExitClientAsync()
        {
            XenoTrayOperationResult result = await _Controller.StopAsync(CancellationToken.None).ConfigureAwait(false);
            LastMessage = result.Message;
            await RefreshAsync(CancellationToken.None).ConfigureAwait(false);
        }

        private async Task OpenDashboardAsync()
        {
            XenoTrayOperationResult result = await _Controller.OpenDashboardAsync(CancellationToken.None).ConfigureAwait(false);
            LastMessage = result.Message;
        }

        private async Task ShowStatisticsAsync()
        {
            XenoTrayStatistics statistics = await _Controller.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);
            string text = XenoTrayStatusFormatter.BuildStatisticsText(_Status, statistics);
            await Dispatcher.UIThread.InvokeAsync(delegate
            {
                if (_StatisticsWindow == null || !_StatisticsWindow.IsVisible)
                {
                    _StatisticsWindow = new StatisticsWindow();
                }

                _StatisticsWindow.SetStatistics(text);
                _StatisticsWindow.Show();
                _StatisticsWindow.Activate();
            });
        }

        private async Task RefreshAsync(CancellationToken token)
        {
            XenoTrayStatus status = await _Controller.GetStatusAsync(token).ConfigureAwait(false);
            XenoTrayMenuState menuState = XenoTrayStatusFormatter.BuildMenuState(status);
            await Dispatcher.UIThread.InvokeAsync(delegate
            {
                _Status = status;
                StatusHeader = menuState.StatusHeader;
                ToolTip = menuState.ToolTip;
                StartEnabled = menuState.StartEnabled;
                OpenDashboardEnabled = menuState.OpenDashboardEnabled;
                ExitClientEnabled = menuState.ExitClientEnabled;
            });
        }

        private static void RunAsync(Func<Task> action)
        {
            Task.Run(async delegate
            {
                try
                {
                    await action().ConfigureAwait(false);
                }
                catch
                {
                }
            });
        }

        private void QuitTray()
        {
            Dispose();
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
        }

        private void SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
        {
            if (object.Equals(field, value)) return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

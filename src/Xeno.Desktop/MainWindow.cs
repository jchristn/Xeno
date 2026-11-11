namespace Xeno.Desktop
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Controls.Shapes;
    using Avalonia.Input;
    using Avalonia.Layout;
    using Avalonia.Media;
    using Avalonia.Media.Imaging;
    using Avalonia.Threading;
    using Xeno.Client.Hosting;
    using Xeno.Client.Presentation;
    using Xeno.Client.Services;
    using Xeno.Desktop.Common;
    using Xeno.Desktop.Pages;

    /// <summary>
    /// The desktop app's main window: a sidebar with the shared navigation catalog and client status, a page header,
    /// the active page, and a status bar. The layout rebuilds when the theme changes.
    /// </summary>
    public sealed class MainWindow : Window
    {
        #region Private-Members

        private readonly DispatcherTimer _AutoRefresh = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        private DateTime _LastAutoRefresh = DateTime.UtcNow;
        private readonly DesktopSession _Session;
        private readonly App _App;
        private readonly Dictionary<XenoPageEnum, DesktopPage> _Pages = new Dictionary<XenoPageEnum, DesktopPage>();
        private readonly Dictionary<XenoPageEnum, Button> _NavButtons = new Dictionary<XenoPageEnum, Button>();
        private XenoPageEnum _Current;
        private ContentControl _PageHost = new ContentControl();
        private StackPanel _HeaderActions = new StackPanel();
        private TextBlock _HeaderTitle = new TextBlock();
        private TextBlock _HeaderDescription = new TextBlock();
        private Ellipse _StatusDot = new Ellipse();
        private TextBlock _StatusText = new TextBlock();
        private TextBlock _StatusDetail = new TextBlock();
        private Button _StatusAction = new Button();
        private TextBlock _StatusBar = new TextBlock();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the main window.
        /// </summary>
        /// <param name="session">Session.</param>
        /// <param name="app">Application, used to execute commands.</param>
        /// <param name="startPage">Page to show first.</param>
        /// <exception cref="ArgumentNullException">Session or app is null.</exception>
        public MainWindow(DesktopSession session, App app, XenoPageEnum startPage)
        {
            _Session = session ?? throw new ArgumentNullException(nameof(session));
            _App = app ?? throw new ArgumentNullException(nameof(app));
            _Current = startPage;
            Title = XenoProduct.DesktopName;
            Width = 1240;
            Height = 800;
            MinWidth = 900;
            MinHeight = 560;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Icon = XenoAssets.LoadWindowIcon();

            BuildLayout();
            InstallKeyBindings();
            XenoTheme.Changed += (sender, args) => BuildLayout();
            _Session.Changed += (sender, args) => OnSessionChanged();
            Opened += (sender, args) => Navigate(_Current);
            _AutoRefresh.Tick += (sender, args) => AutoRefreshTick();
            Opened += (sender, args) => _AutoRefresh.Start();
            Closed += (sender, args) => _AutoRefresh.Stop();
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// The page on screen, or null before the first navigation.
        /// </summary>
        public DesktopPage? CurrentPage
        {
            get { return _Pages.TryGetValue(_Current, out DesktopPage? page) ? page : null; }
        }

        /// <summary>
        /// Show a page.
        /// </summary>
        /// <param name="page">Page.</param>
        public void Navigate(XenoPageEnum page)
        {
            if (_Pages.TryGetValue(_Current, out DesktopPage? previous) && _Current != page) previous.OnDeactivated();
            _Current = page;
            _Session.Preferences.DesktopLastPage = page;
            _Session.SavePreferences();
            DesktopPage view = GetPage(page);
            XenoPageDefinition definition = XenoNavigationCatalog.Get(page);
            Title = XenoProduct.DesktopName + " - " + definition.Title;
            _HeaderTitle.Text = definition.LongTitle;
            _HeaderDescription.Text = definition.Description;
            _HeaderActions.Children.Clear();
            foreach (Control action in view.Actions()) _HeaderActions.Children.Add(action);
            Control content = view.Build();
            _PageHost.Content = view.Scrolls ? new ScrollViewer { Content = content, Padding = new Thickness(0, 0, 8, 0) } : content;
            UpdateNav();
            _ = view.RefreshAsync();
        }

        /// <summary>
        /// Run a shared menu command, as the menu or a shortcut would.
        /// </summary>
        /// <param name="command">Command.</param>
        public void Execute(XenoCommandEnum command)
        {
            _App.Execute(command);
        }

        /// <summary>
        /// Reload the current page.
        /// </summary>
        public void RefreshCurrent()
        {
            _ = _Session.RefreshStatusAsync();
            if (_Pages.TryGetValue(_Current, out DesktopPage? page)) _ = page.RefreshAsync();
        }

        #endregion

        #region Private-Methods

        private DesktopPage GetPage(XenoPageEnum page)
        {
            if (_Pages.TryGetValue(page, out DesktopPage? existing)) return existing;
            DesktopPage created;
            switch (page)
            {
                case XenoPageEnum.Chat: created = new ChatPage(_Session, this); break;
                case XenoPageEnum.ChatHistory: created = new ChatHistoryPage(_Session, this); break;
                case XenoPageEnum.Usage: created = new UsagePage(_Session, this); break;
                case XenoPageEnum.ChatFeedback: created = new ChatFeedbackPage(_Session, this); break;
                case XenoPageEnum.Models: created = new ModelsPage(_Session, this); break;
                case XenoPageEnum.Providers: created = new ProvidersPage(_Session, this); break;
                case XenoPageEnum.LocalEndpoint: created = new LocalEndpointPage(_Session, this); break;
                case XenoPageEnum.Requests: created = new RequestsPage(_Session, this); break;
                case XenoPageEnum.Logs: created = new LogsPage(_Session, this); break;
                case XenoPageEnum.HostTelemetry: created = new HostTelemetryPage(_Session, this); break;
                case XenoPageEnum.ProviderTelemetry: created = new ProviderTelemetryPage(_Session, this); break;
                case XenoPageEnum.Server: created = new ServerPage(_Session, this); break;
                case XenoPageEnum.Settings: created = new SettingsPage(_Session, this); break;
                default: created = new OverviewPage(_Session, this, _App); break;
            }

            _Pages[page] = created;
            return created;
        }

        private void BuildLayout()
        {
            XenoPalette palette = XenoTheme.Current;
            Background = palette.Background;
            _NavButtons.Clear();

            DockPanel root = new DockPanel();
            if (!OperatingSystem.IsMacOS())
            {
                NativeMenuBar menuBar = new NativeMenuBar();
                DockPanel.SetDock(menuBar, Dock.Top);
                root.Children.Add(menuBar);
            }

            _StatusBar = new TextBlock { Foreground = palette.Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            Border statusBar = new Border
            {
                Background = palette.Panel,
                BorderBrush = palette.Border,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(14, 5),
                Child = _StatusBar
            };
            DockPanel.SetDock(statusBar, Dock.Bottom);
            root.Children.Add(statusBar);

            Grid body = new Grid { ColumnDefinitions = new ColumnDefinitions("230,*") };
            Control sidebar = BuildSidebar(palette);
            Grid.SetColumn(sidebar, 0);
            body.Children.Add(sidebar);

            DockPanel main = new DockPanel { Margin = new Thickness(24, 18, 16, 12) };
            DockPanel header = new DockPanel { Margin = new Thickness(0, 0, 8, 16) };
            _HeaderActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(_HeaderActions, Dock.Right);
            header.Children.Add(_HeaderActions);
            _HeaderTitle = Ui.Title(String.Empty);
            _HeaderDescription = Ui.Muted(String.Empty);
            header.Children.Add(Ui.Stack(2, _HeaderTitle, _HeaderDescription));
            DockPanel.SetDock(header, Dock.Top);
            main.Children.Add(header);
            _PageHost = new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
            main.Children.Add(_PageHost);
            Grid.SetColumn(main, 1);
            body.Children.Add(main);
            root.Children.Add(body);
            Content = root;

            OnSessionChanged();
            if (IsVisible) Navigate(_Current);
        }

        private Control BuildSidebar(XenoPalette palette)
        {
            DockPanel sidebar = new DockPanel();
            StackPanel brand = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(18, 18, 18, 14) };
            Bitmap? logo = XenoAssets.LoadLogo();
            if (logo != null) brand.Children.Add(new Image { Source = logo, Width = 30, Height = 30 });
            brand.Children.Add(new TextBlock { Text = XenoProduct.Name, FontSize = 20, FontWeight = FontWeight.SemiBold, Foreground = palette.SidebarText, VerticalAlignment = VerticalAlignment.Center });
            DockPanel.SetDock(brand, Dock.Top);
            sidebar.Children.Add(brand);

            Border statusCard = BuildStatusCard(palette);
            DockPanel.SetDock(statusCard, Dock.Bottom);
            sidebar.Children.Add(statusCard);

            StackPanel nav = new StackPanel { Spacing = 2, Margin = new Thickness(10, 4) };
            foreach (string section in XenoNavigationCatalog.Sections)
            {
                nav.Children.Add(new TextBlock
                {
                    Text = section.ToUpperInvariant(),
                    FontSize = 10.5,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = palette.SidebarMuted,
                    Margin = new Thickness(10, 12, 0, 4)
                });

                foreach (XenoPageDefinition page in XenoNavigationCatalog.Pages.Where(item => item.Section == section))
                {
                    XenoPageEnum target = page.Page;
                    Button button = new Button
                    {
                        Content = page.Title,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        HorizontalContentAlignment = HorizontalAlignment.Left,
                        Padding = new Thickness(12, 7),
                        Background = Brushes.Transparent,
                        BorderThickness = new Thickness(0),
                        Foreground = palette.SidebarText
                    };
                    ToolTip.SetTip(button, page.Description);
                    button.Click += (sender, args) => Navigate(target);
                    _NavButtons[target] = button;
                    nav.Children.Add(button);
                }
            }

            sidebar.Children.Add(new ScrollViewer { Content = nav });
            return new Border { Background = palette.Sidebar, Child = sidebar };
        }

        private Border BuildStatusCard(XenoPalette palette)
        {
            _StatusDot = new Ellipse { Width = 9, Height = 9, Fill = palette.SidebarMuted, VerticalAlignment = VerticalAlignment.Center };
            _StatusText = new TextBlock { Text = "Checking...", Foreground = XenoTheme.Current.SidebarText, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            _StatusDetail = new TextBlock { Foreground = palette.SidebarMuted, FontSize = 11.5, TextWrapping = TextWrapping.Wrap };
            _StatusAction = new Button { HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center, Padding = new Thickness(10, 5) };
            _StatusAction.Classes.Add("accent");
            _StatusAction.Click += (sender, args) => RunStatusAction();
            StackPanel stack = new StackPanel { Spacing = 8 };
            stack.Children.Add(Ui.Row(_StatusDot, _StatusText));
            stack.Children.Add(_StatusDetail);
            stack.Children.Add(_StatusAction);
            return new Border
            {
                Background = new SolidColorBrush(((ISolidColorBrush)palette.SidebarText).Color, 0.11),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12),
                Margin = new Thickness(12),
                Child = stack
            };
        }

        private void RunStatusAction()
        {
            ClientStatusSnapshot? status = _Session.Status;
            if (status == null) return;
            if (status.Status == ClientStatusEnum.NotSignedIn) _App.Execute(XenoCommandEnum.SignIn);
            else if (status.Running) _App.Execute(XenoCommandEnum.StopClient);
            else _App.Execute(XenoCommandEnum.StartClient);
        }

        private void AutoRefreshTick()
        {
            int seconds = _Session.Preferences.AutoRefreshSeconds;
            if (seconds <= 0 || (DateTime.UtcNow - _LastAutoRefresh).TotalSeconds < seconds) return;
            _LastAutoRefresh = DateTime.UtcNow;

            // An open dialog pauses refreshing so nothing moves underneath it.
            if (OwnedWindows.Count > 0) return;
            DesktopPage? page = CurrentPage;
            if (page != null && page.AutoRefreshes) _ = page.RefreshAsync();
        }

        private void OnSessionChanged()
        {
            XenoPalette palette = XenoTheme.Current;
            ClientStatusSnapshot? status = _Session.Status;
            _StatusBar.Text = String.IsNullOrWhiteSpace(_Session.Message) ? (status == null ? "Loading..." : status.StatusDetail) : _Session.Message;
            if (status == null) return;
            _StatusText.Text = _Session.Busy ? "Working..." : status.StatusText;
            _StatusDetail.Text = DisplayFormat.OrDash(status.Name) + " • " + DisplayFormat.OrDash(status.ServerLabel) + " • " + status.ModelCount + " model" + (status.ModelCount == 1 ? String.Empty : "s");
            switch (status.Status)
            {
                case ClientStatusEnum.Connected: _StatusDot.Fill = palette.Success; break;
                case ClientStatusEnum.Starting:
                case ClientStatusEnum.Reconnecting: _StatusDot.Fill = palette.Warning; break;
                case ClientStatusEnum.NotSignedIn: _StatusDot.Fill = palette.Danger; break;
                default: _StatusDot.Fill = palette.SidebarMuted; break;
            }

            _StatusAction.Content = status.Status == ClientStatusEnum.NotSignedIn ? "Sign In..." : status.Running ? "Stop Client" : "Start Client";
            _StatusAction.IsEnabled = !_Session.Busy;
            if (_Pages.TryGetValue(_Current, out DesktopPage? page)) page.OnStatusChanged();
        }

        private void UpdateNav()
        {
            XenoPalette palette = XenoTheme.Current;
            foreach (KeyValuePair<XenoPageEnum, Button> pair in _NavButtons)
            {
                bool active = pair.Key == _Current;
                pair.Value.Background = active ? palette.SidebarActive : Brushes.Transparent;
                pair.Value.Foreground = active ? palette.SidebarActiveText : palette.SidebarText;
                pair.Value.FontWeight = active ? FontWeight.SemiBold : FontWeight.Normal;
            }
        }

        private void InstallKeyBindings()
        {
            // macOS delivers shortcuts through the native menu; elsewhere the in-window menu shows them, and these
            // bindings make them work.
            if (OperatingSystem.IsMacOS()) return;
            foreach (XenoMenuEntry entry in XenoMenuLayout.MenuBar(XenoSurfaceEnum.Desktop, false).SelectMany(item => item.Flatten()))
            {
                if (entry.Kind != XenoMenuEntryKindEnum.Item || String.IsNullOrEmpty(entry.Key)) continue;
                KeyGesture? gesture = XenoNativeMenuBuilder.ToGesture(entry.Key, entry.Shift);
                if (gesture == null) continue;
                XenoCommandEnum command = entry.Command;
                KeyBindings.Add(new KeyBinding { Gesture = gesture, Command = new DelegateCommand(() => _App.Execute(command), () => _App.CanExecute(command)) });
            }
        }

        #endregion
    }
}

namespace Xeno.Tui
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using TUIKit;
    using TUIKit.Hosting;
    using TUIKit.Input;
    using TUIKit.Layout;
    using TUIKit.Modals;
    using TUIKit.Terminal;
    using TUIKit.Theming;
    using TUIKit.Widgets;
    using Xeno.Client;
    using Xeno.Client.Hosting;
    using Xeno.Client.Presentation;
    using Xeno.Client.Services;
    using Xeno.Tui.Modals;
    using Xeno.Tui.Pages;
    using Xeno.Tui.Shell;

    /// <summary>
    /// The Xeno terminal app (xeno tui): a header, a menu bar, a navigation sidebar, the current page, and a status bar,
    /// over the same <see cref="XenoWorkspace"/>, menus, navigation, forms, and settings as the desktop app.
    /// </summary>
    public sealed class XenoTuiApp : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Page shown in the main region.
        /// </summary>
        public XenoPageEnum CurrentPage { get; private set; } = XenoPageEnum.Overview;

        /// <summary>
        /// Smallest terminal width the app lays out in. Below it (or below <see cref="MinimumHeight"/>), TUIKit shows its
        /// "terminal too small" screen until the window grows. The app is designed for a large terminal.
        /// </summary>
        public const int MinimumWidth = 120;

        /// <summary>
        /// Smallest terminal height the app lays out in (see <see cref="MinimumWidth"/>).
        /// </summary>
        public const int MinimumHeight = 36;

        /// <summary>
        /// Completes when the first status refresh and page load finish.
        /// </summary>
        public Task StartupTask { get; private set; } = Task.CompletedTask;

        #endregion

        #region Internal-Members

        /// <summary>
        /// Shared context.
        /// </summary>
        internal TuiContext Context { get; }

        /// <summary>
        /// Registered commands (palette, key help, and chords).
        /// </summary>
        internal IReadOnlyList<Command> Commands
        {
            get { return _Commands; }
        }

        /// <summary>
        /// Menu bar.
        /// </summary>
        internal MenuBar MenuBar
        {
            get { return _MenuHost.Bar; }
        }

        /// <summary>
        /// Current page view.
        /// </summary>
        internal TuiPage CurrentView
        {
            get { return GetPage(CurrentPage); }
        }

        #endregion

        #region Private-Members

        private const string MainRegion = "main";
        private const int MenuRow = 1;
        private const int SpinnerIntervalMs = 100;
        private const string SidebarRegion = "sidebar";
        private readonly TuiApplication _App;
        private readonly TuiStartOptions _Options;
        private readonly bool _OwnsWorkspace;
        private readonly HeaderView _Header = new HeaderView();
        private readonly MenuBarHost _MenuHost = new MenuBarHost();
        private readonly SidebarView _Sidebar = new SidebarView();
        private readonly PageHost _PageHost = new PageHost();
        private readonly StatusBar _StatusBar = new StatusBar();
        private readonly StatusBarHost _StatusHost;
        private readonly MessageBar _MessageBar;
        private readonly Dictionary<XenoPageEnum, TuiPage> _Pages = new Dictionary<XenoPageEnum, TuiPage>();
        private readonly Dictionary<XenoCommandEnum, MenuItem> _MenuItems = new Dictionary<XenoCommandEnum, MenuItem>();
        private readonly List<Command> _Commands = new List<Command>();
        private readonly CancellationTokenSource _Stop = new CancellationTokenSource();
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the terminal app on a TUIKit host.
        /// </summary>
        /// <param name="app">TUIKit host.</param>
        /// <param name="options">Start options.</param>
        /// <param name="workspace">Workspace, or null to create one owned by the app.</param>
        /// <exception cref="ArgumentNullException">App or options is null.</exception>
        public XenoTuiApp(TuiApplication app, TuiStartOptions options, XenoWorkspace? workspace = null)
        {
            _App = app ?? throw new ArgumentNullException(nameof(app));
            _Options = options ?? throw new ArgumentNullException(nameof(options));
            _OwnsWorkspace = workspace == null;
            XenoFrontEndPreferences preferences = XenoFrontEndPreferences.Load(options.PreferencesPath);
            Context = new TuiContext(app, workspace ?? new XenoWorkspace(), preferences, options.PreferencesPath)
            {
                Execute = Execute,
                Navigate = Navigate
            };

            _MessageBar = new MessageBar(Context);
            _StatusHost = new StatusBarHost(_StatusBar);
            CurrentPage = options.StartPage ?? preferences.TerminalLastPage;
            BuildCommands();
            Wire();
            ApplyAppearance(preferences.TerminalAppearance);
            Navigate(CurrentPage);
            if (!String.IsNullOrWhiteSpace(options.StartupMessage)) Context.Notify(options.StartupMessage, NotificationSeverity.Info);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Run the terminal app on the console until the user quits.
        /// </summary>
        /// <param name="options">Start options.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Exit code.</returns>
        public static async Task<int> RunConsoleAsync(TuiStartOptions options, CancellationToken token)
        {
            using (ConsoleBackend backend = new ConsoleBackend())
            using (TuiApplication app = new TuiApplication(backend))
            using (XenoTuiApp tui = new XenoTuiApp(app, options))
            {
                tui.Start();
                await app.RunAsync(token).ConfigureAwait(false);
                return 0;
            }
        }

        /// <summary>
        /// Load status and the first page, and start the refresh loop when live.
        /// </summary>
        public void Start()
        {
            StartupTask = RefreshStatusAndPageAsync(true);
            if (_Options.Live)
            {
                _ = RefreshLoopAsync(_Stop.Token);
                _ = SpinnerLoopAsync(_Stop.Token);
            }
        }

        /// <summary>
        /// Show a page.
        /// </summary>
        /// <param name="page">Page.</param>
        public void Navigate(XenoPageEnum page)
        {
            CurrentPage = page;
            TuiPage view = GetPage(page);
            _PageHost.Content = view.Root;
            _PageHost.Fallback = view.HandleKey;
            _PageHost.Hints = view.Hints;
            _Sidebar.Current = page;
            _Sidebar.Highlight(page);
            Context.Preferences.TerminalLastPage = page;
            Context.SavePreferences();
            _App.Focus(MainRegion);
            _ = view.RefreshAsync();
        }

        /// <summary>
        /// Determine whether a command is enabled.
        /// </summary>
        /// <param name="command">Command.</param>
        /// <returns>True when enabled.</returns>
        public bool CanExecute(XenoCommandEnum command)
        {
            if (Context.Busy && (command == XenoCommandEnum.StartClient || command == XenoCommandEnum.StopClient || command == XenoCommandEnum.RestartClient)) return false;
            return XenoCommandAvailability.IsEnabled(command, Context.Status);
        }

        /// <summary>
        /// Execute a shared menu command.
        /// </summary>
        /// <param name="command">Command.</param>
        public void Execute(XenoCommandEnum command)
        {
            XenoPageEnum? page = XenoNavigationCatalog.PageFor(command);
            if (page != null)
            {
                Navigate(page.Value);
                return;
            }

            XenoAppearanceEnum? appearance = XenoCommandAvailability.AppearanceFor(command);
            if (appearance != null)
            {
                Context.Preferences.TerminalAppearance = appearance.Value;
                Context.SavePreferences();
                ApplyAppearance(appearance.Value);
                return;
            }

            string? link = XenoCommandAvailability.LinkFor(command);
            if (link != null)
            {
                Context.Notify(XenoAppLauncher.OpenUrl(link).Message, NotificationSeverity.Info);
                return;
            }

            if (!CanExecute(command))
            {
                Context.Notify(XenoCommandAvailability.DisabledReason(command, Context.Status) ?? "Not available right now.", NotificationSeverity.Warning);
                return;
            }

            switch (command)
            {
                case XenoCommandEnum.Settings:
                    Navigate(XenoPageEnum.Settings);
                    break;
                case XenoCommandEnum.Quit:
                    _App.Quit();
                    break;
                case XenoCommandEnum.Refresh:
                    _ = RefreshStatusAndPageAsync(true);
                    break;
                case XenoCommandEnum.NewProvider:
                    _ = Context.FormAsync(XenoForms.AddProvider(Context.Workspace), RefreshCurrent);
                    break;
                case XenoCommandEnum.NewModel:
                    XenoFormSpec? model = XenoForms.AddModel(Context.Workspace, null);
                    if (model == null) Context.Notify("Add a provider first. Models are served by a provider endpoint on this machine.", NotificationSeverity.Warning);
                    else _ = Context.FormAsync(model, RefreshCurrent);
                    break;
                case XenoCommandEnum.OpenSettingsFile:
                    _ = EditSettingsFileAsync();
                    break;
                case XenoCommandEnum.RevealDataFolder:
                    Context.Notify(XenoAppLauncher.OpenUrl(ClientSettings.SettingsDirectory).Message, NotificationSeverity.Info);
                    break;
                case XenoCommandEnum.StartClient:
                    RunClientOperation("Starting the client...", token => XenoClientActions.StartAsync(Context.Workspace, token));
                    break;
                case XenoCommandEnum.StopClient:
                    RunClientOperation("Stopping the client...", token => XenoClientActions.StopAsync(Context.Workspace, token));
                    break;
                case XenoCommandEnum.RestartClient:
                    RunClientOperation("Restarting the client...", token => XenoClientActions.RestartAsync(Context.Workspace, token));
                    break;
                case XenoCommandEnum.ReloadConfiguration:
                    Context.Notify(XenoClientActions.RequestInventoryUpdate(Context.Workspace), NotificationSeverity.Info);
                    break;
                case XenoCommandEnum.SignIn:
                    _ = Context.SignInAsync(() => _ = RefreshStatusAndPageAsync(true));
                    break;
                case XenoCommandEnum.TestConnection:
                    Context.Progress("Testing the server connection...");
                    _ = Context.RunAsync(token => XenoClientActions.PingAsync(Context.Workspace, 3, token));
                    break;
                case XenoCommandEnum.OpenWebDashboard:
                    Context.Notify(XenoClientActions.OpenWebDashboard(), NotificationSeverity.Info);
                    break;
                case XenoCommandEnum.OpenDesktopApp:
                    Context.Notify(XenoClientActions.OpenDesktopApp(), NotificationSeverity.Info);
                    break;
                case XenoCommandEnum.About:
                    _ = Context.ShowMarkdownAsync("About " + XenoProduct.TerminalName, XenoDiagnostics.BuildMarkdown(XenoProduct.TerminalName, Context.Status ?? Context.Workspace.GetStatus()));
                    break;
                case XenoCommandEnum.KeyboardShortcuts:
                    _ = ShowKeyHelpAsync();
                    break;
                case XenoCommandEnum.CommandPalette:
                    _ = ShowPaletteAsync();
                    break;
            }
        }

        /// <summary>
        /// Stop background refresh and release the workspace when owned.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;
            _Stop.Cancel();
            _Stop.Dispose();
            if (_OwnsWorkspace) Context.Workspace.Dispose();
        }

        #endregion

        #region Private-Methods

        private TuiPage GetPage(XenoPageEnum page)
        {
            if (_Pages.TryGetValue(page, out TuiPage? existing)) return existing;
            TuiPage created;
            switch (page)
            {
                case XenoPageEnum.Models: created = new ModelsPage(Context); break;
                case XenoPageEnum.Providers: created = new ProvidersPage(Context); break;
                case XenoPageEnum.LocalEndpoint: created = new LocalEndpointPage(Context); break;
                case XenoPageEnum.Requests: created = new RequestsPage(Context); break;
                case XenoPageEnum.Logs: created = new LogsPage(Context); break;
                case XenoPageEnum.HostTelemetry: created = new HostTelemetryPage(Context); break;
                case XenoPageEnum.ProviderTelemetry: created = new ProviderTelemetryPage(Context); break;
                case XenoPageEnum.Server: created = new ServerPage(Context); break;
                case XenoPageEnum.Settings: created = new SettingsPage(Context); break;
                default: created = new OverviewPage(Context); break;
            }

            _Pages[page] = created;
            return created;
        }

        private void Wire()
        {
            // The main region's minimum extents set the layout's minimum size; below it TUIKit shows "terminal too small".
            _App.Layout = Layout.Create()
                .Add("header", r => r.FillWidth().TopAnchored(0, 1).WithPadding(0))
                .Add("menu", r => r.FillWidth().TopAnchored(MenuRow, 1).WithPadding(0))
                .Add(SidebarRegion, r => r.LeftAnchored(0, 24).FillHeight(2, 2).WithBorder(BorderStyle.Rounded, "Xeno").WithPadding(0))
                .Add(MainRegion, r => r.Horizontal(AxisConstraint.Stretch(24, 0, MinimumWidth - 24)).Vertical(AxisConstraint.Stretch(2, 2, MinimumHeight - 4)).WithPadding(0))
                .Add("message", r => r.FillWidth().BottomAnchored(1, 1).WithPadding(0))
                .Add("status", r => r.FillWidth().BottomAnchored(0, 1).WithPadding(0))
                .Build();

            // Bind order is focus order: the page first, then the sidebar.
            _App.Bind(MainRegion, _PageHost);
            _App.Bind(SidebarRegion, _Sidebar);
            _App.Bind("header", _Header);
            _App.Bind("menu", _MenuHost);
            _App.Bind("message", _MessageBar);
            _App.Bind("status", _StatusHost);
            _App.HighlightFocusedRegion = true;
            _App.CtrlCPolicy = CtrlCPolicy.DoubleTapToExit;
            // A paste outside a dialog goes to the focused search field.
            _App.RoutePasteToFocus = true;
            _Sidebar.PageChosen += Navigate;

            KeyHintResolver hints = _App.BindKeyHints(_StatusBar);
            hints.AddAppHint("F10", "Menu");
            hints.AddAppHint("Ctrl+K", "Palette");
            hints.AddAppHint("F1", "Help");
            hints.AddAppHint("Ctrl+Q", "Quit");
            _StatusBar.RightText = String.Empty;

            _App.KeyFilter = key =>
            {
                if (key.Code == KeyCode.F10)
                {
                    OpenMenu();
                    return true;
                }

                return false;
            };
            _MenuHost.Opened = ShowMenu;
            _StatusHost.HintClicked = RunHint;
            Context.Locate = Locate;

            _App.RenderOverlay = surface =>
            {
                _Header.Status = Context.Status;
                _Header.Busy = Context.Busy;
            };
        }

        private void RunHint(KeyHint hint)
        {
            // A clicked hint runs its key as if it were pressed: F10 and app commands first, then the focused pane.
            KeyEvent? parsed = KeyOf(hint);
            if (parsed == null) return;
            KeyEvent key = parsed.Value;
            if (key.Code == KeyCode.F10)
            {
                OpenMenu();
                return;
            }

            KeyChord chord = KeyChord.FromKeyEvent(key);
            Command? command = _Commands.FirstOrDefault(item => item.Chord != null && item.Chord.Value.Equals(chord));
            if (command != null)
            {
                if (command.IsEnabled) command.Handler();
                return;
            }

            if (_App.FocusedRegion == SidebarRegion) _Sidebar.HandleKey(key);
            else _PageHost.HandleKey(key);
        }

        private static KeyEvent? KeyOf(KeyHint hint)
        {
            KeyChord chord;
            if (hint.Chord != null) chord = hint.Chord.Value;
            else
            {
                try
                {
                    chord = KeyChord.Parse(hint.Key);
                }
                catch (FormatException)
                {
                    return null;
                }
                catch (ArgumentException)
                {
                    return null;
                }
            }

            return new KeyEvent(chord.Code, chord.Rune, chord.Modifiers);
        }

        private void OpenMenu()
        {
            if (!MenuBar.IsOpen) MenuBar.HandleKey(KeyEvent.Special(KeyCode.Enter));
            ShowMenu();
        }

        private void ShowMenu()
        {
            // While a menu is open it is a modal, so clicks on its drop-down never reach the page underneath.
            if (!MenuBar.IsOpen || _App.Modals.IsActive) return;
            _ = _App.ShowAsync(new MenuBarModal(MenuBar, MenuRow));
        }

        private Rect? Locate(IWidget target)
        {
            Rect? bounds = _App.GetRegionContentBounds(MainRegion);
            if (bounds == null) return null;
            return Locate(CurrentView.Root, bounds.Value, target);
        }

        private static Rect? Locate(IWidget node, Rect rect, IWidget target)
        {
            if (ReferenceEquals(node, target)) return rect;
            if (node is FramedStack stack)
            {
                foreach (IWidget child in stack.Children)
                {
                    Rect content = stack.ContentRectOf(child);
                    if (content.Width <= 0 || content.Height <= 0) continue;
                    Rect? found = Locate(child, new Rect(rect.X + content.X, rect.Y + content.Y, content.Width, content.Height), target);
                    if (found != null) return found;
                }

                return null;
            }

            IWidget? inner = node is Inset inset ? inset.Child : node is FocusInset focusInset ? focusInset.Child : null;
            if (inner == null) return null;
            return Locate(inner, new Rect(rect.X + Inset.Columns, rect.Y, Math.Max(0, rect.Width - Inset.Columns * 2), rect.Height), target);
        }

        private void BuildCommands()
        {
            _Commands.Clear();
            CommandRegistry registry = new CommandRegistry();
            foreach (XenoMenuEntry menu in XenoMenuLayout.MenuBar(XenoSurfaceEnum.Terminal, false))
            {
                foreach (XenoMenuEntry entry in menu.Flatten().Where(item => item.Kind == XenoMenuEntryKindEnum.Item || item.Kind == XenoMenuEntryKindEnum.Radio))
                {
                    XenoCommandEnum command = entry.Command;
                    KeyChord? chord = Chord(entry);
                    Command item = new Command("xeno." + command, entry.Header.TrimEnd('.'), () => Execute(command), menu.Header, chord, null, () => CanExecute(command));
                    registry.Add(item);
                    _Commands.Add(item);
                }
            }

            registry.ApplyTo(_App);
            BuildMenuBar();
        }

        private void BuildMenuBar()
        {
            MenuBar bar = new MenuBar();
            _MenuItems.Clear();
            foreach (XenoMenuEntry menu in XenoMenuLayout.MenuBar(XenoSurfaceEnum.Terminal, false))
            {
                Menu dropdown = bar.AddMenu(menu.Header);
                AddMenuItems(dropdown, menu.Children, String.Empty);
            }

            ApplyMenuTheme(bar, _App.Theme);
            _MenuHost.Bar = bar;
        }

        private void AddMenuItems(Menu dropdown, IEnumerable<XenoMenuEntry> entries, string prefix)
        {
            foreach (XenoMenuEntry entry in entries)
            {
                if (entry.Kind == XenoMenuEntryKindEnum.Separator)
                {
                    dropdown.Add(new MenuItem(new string('─', 18)) { Enabled = false });
                    continue;
                }

                if (entry.Kind == XenoMenuEntryKindEnum.Submenu)
                {
                    AddMenuItems(dropdown, entry.Children, entry.Header + ": ");
                    continue;
                }

                XenoCommandEnum command = entry.Command;
                string label = prefix + entry.Header;
                if (entry.Kind == XenoMenuEntryKindEnum.Radio)
                {
                    XenoAppearanceEnum? appearance = XenoCommandAvailability.AppearanceFor(command);
                    label = (appearance == Context.Preferences.TerminalAppearance ? "(•) " : "( ) ") + label;
                }

                KeyChord? chord = Chord(entry);
                if (chord != null) label = label.PadRight(28) + chord.Value.ToLabel(KeyLabelStyle.Ascii);
                MenuItem item = new MenuItem(label, () => Execute(command));
                _MenuItems[command] = item;
                dropdown.Add(item);
            }
        }

        private static KeyChord? Chord(XenoMenuEntry entry)
        {
            if (String.IsNullOrEmpty(entry.Key)) return null;
            string key = entry.Key;
            if (key.Length > 1 && key[0] == 'F' && Char.IsDigit(key[1])) return KeyChord.Parse(key.ToLowerInvariant());
            if (entry.Shift) return null;
            if (key.Length == 1 && Char.IsDigit(key[0])) return KeyChord.Parse("alt+" + key);
            if (key.Length == 1 && Char.IsLetter(key[0])) return KeyChord.Parse("ctrl+" + key.ToLowerInvariant());
            return null;
        }

        private void ApplyAppearance(XenoAppearanceEnum appearance)
        {
            Theme theme = appearance == XenoAppearanceEnum.Light ? XenoTuiTheme.Light : XenoTuiTheme.Dark;
            _App.Theme = theme;
            _Header.ApplyTheme(theme);
            _Sidebar.ApplyTheme(theme);
            _PageHost.ApplyTheme(theme);
            foreach (TuiPage page in _Pages.Values)
            {
                if (page.Root is IThemeable themeable) themeable.ApplyTheme(theme);
            }

            _MessageBar.ApplyTheme(theme);
            _StatusBar.KeyStyle = theme.Accent.WithAttribute(CellAttributes.Bold, true);
            _StatusBar.LabelStyle = theme.Muted;
            _StatusBar.RightTextStyle = theme.Muted;
            BuildMenuBar();
        }

        private static void ApplyMenuTheme(MenuBar bar, Theme theme)
        {
            CellStyle barStyle = theme.Resolve(XenoTuiTheme.MenuBarRole, theme.Muted.WithAttribute(CellAttributes.Reverse, true));
            bar.BarStyle = barStyle;
            bar.ActiveStyle = theme.Selection;
            bar.ItemStyle = barStyle;
            bar.DisabledStyle = theme.Disabled;
            bar.DropdownStyle = theme.Text;
            bar.DropdownBorderStyle = theme.Border;
        }

        private void RefreshMenuState()
        {
            foreach (KeyValuePair<XenoCommandEnum, MenuItem> pair in _MenuItems)
            {
                pair.Value.Enabled = CanExecute(pair.Key);
            }
        }

        private void RefreshCurrent()
        {
            _ = CurrentView.RefreshAsync();
        }

        private async Task RefreshStatusAndPageAsync(bool includePage)
        {
            ClientStatusSnapshot status = await Task.Run(() => Context.Workspace.GetStatus()).ConfigureAwait(false);
            TaskCompletionSource<Task> applied = new TaskCompletionSource<Task>(TaskCreationOptions.RunContinuationsAsynchronously);
            _App.Post(() =>
            {
                Context.Status = status;
                RefreshMenuState();
                CurrentView.OnStatusChanged(status);
                if (String.IsNullOrWhiteSpace(Context.Message)) Context.Report(status.StatusDetail);

                // Start the page refresh here, on the loop thread: pages update their widgets before the first await,
                // and doing that from a pool thread races the renderer ("Collection was modified").
                applied.TrySetResult(includePage ? CurrentView.RefreshAsync() : Task.CompletedTask);
            });

            if (includePage)
            {
                Task pageRefresh = await applied.Task.ConfigureAwait(false);
                await pageRefresh.ConfigureAwait(false);
            }
        }

        private async Task RefreshLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(Context.Preferences.RefreshSeconds), token).ConfigureAwait(false);
                    await RefreshStatusAndPageAsync(CurrentView.RefreshWithStatus).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex) when (ex is System.IO.IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException)
                {
                    _App.Post(() => Context.Report("Refresh failed: " + ex.Message));
                }
            }
        }

        private void RunClientOperation(string progress, Func<CancellationToken, Task<string>> operation)
        {
            if (Context.Busy) return;
            Context.Busy = true;
            Context.Progress(progress);
            RefreshMenuState();
            _ = Task.Run(async () =>
            {
                string message;
                try
                {
                    message = await operation(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException || ex is System.IO.IOException)
                {
                    message = ex.Message;
                }

                _App.Post(() =>
                {
                    Context.Busy = false;
                    Context.Notify(message, TuiContext.SeverityOf(message));
                });
                await RefreshStatusAndPageAsync(true).ConfigureAwait(false);
            });
        }

        private async Task SpinnerLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(SpinnerIntervalMs, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                if (Context.Working || Context.Busy) _App.RequestRender();
            }
        }

        private async Task ShowPaletteAsync()
        {
            CommandPaletteModal palette = new CommandPaletteModal(_Commands.Where(item => item.Id != "xeno." + XenoCommandEnum.CommandPalette), "Xeno Commands");
            palette.ApplyTheme(_App.Theme);
            await _App.ShowAsync(palette).ConfigureAwait(false);
        }

        private async Task ShowKeyHelpAsync()
        {
            KeyHelpModal help = KeyHelpModal.FromCommands(_Commands);
            help.Add("Navigation", "Tab / Shift+Tab", "Move between the page, its fields, and the sidebar");
            help.Add("Navigation", "F10", "Open the menu bar");
            help.Add("Navigation", "Enter", "Open the selected row or page");
            foreach (XenoPageEnum page in Enum.GetValues<XenoPageEnum>())
            {
                foreach (KeyHint hint in GetPage(page).Hints)
                {
                    help.Add(XenoNavigationCatalog.Get(page).LongTitle, hint.Key, hint.Description);
                }
            }

            help.ApplyTheme(_App.Theme);
            await _App.ShowAsync(help).ConfigureAwait(false);
        }

        private async Task EditSettingsFileAsync()
        {
            string? editor = Environment.GetEnvironmentVariable("VISUAL");
            if (String.IsNullOrWhiteSpace(editor)) editor = Environment.GetEnvironmentVariable("EDITOR");
            if (String.IsNullOrWhiteSpace(editor))
            {
                _App.Post(() => Context.Notify(XenoAppLauncher.OpenUrl(ClientSettings.SettingsFile).Message, NotificationSeverity.Info));
                return;
            }

            TaskCompletionSource<bool> done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _App.Post(() => _ = SuspendForEditorAsync(editor, done));
            await done.Task.ConfigureAwait(false);
            await RefreshStatusAndPageAsync(true).ConfigureAwait(false);
        }

        private async Task SuspendForEditorAsync(string editor, TaskCompletionSource<bool> done)
        {
            try
            {
                await _App.SuspendAsync(async () =>
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo(editor) { UseShellExecute = false };
                    startInfo.ArgumentList.Add(ClientSettings.SettingsFile);
                    using (Process? process = Process.Start(startInfo))
                    {
                        if (process != null) await process.WaitForExitAsync().ConfigureAwait(false);
                    }
                }).ConfigureAwait(true);
                Context.Notify("Reloaded " + ClientSettings.SettingsFile + ".", NotificationSeverity.Info);
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception || ex is InvalidOperationException)
            {
                Context.Notify("Could not start " + editor + ": " + ex.Message, NotificationSeverity.Error);
            }
            finally
            {
                done.TrySetResult(true);
            }
        }

        #endregion
    }
}

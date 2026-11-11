namespace Xeno.Desktop.Pages
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Threading;
    using Xeno.Client;
    using Xeno.Client.Hosting;
    using Xeno.Client.Presentation;
    using Xeno.Client.Services;
    using Xeno.Desktop.Common;

    /// <summary>
    /// Logs: the client worker log or error log, with tail length, severity filter, and live follow.
    /// </summary>
    public sealed class LogsPage : DesktopPage
    {
        #region Public-Members

        /// <inheritdoc />
        public override XenoPageEnum Page
        {
            get { return XenoPageEnum.Logs; }
        }

        /// <inheritdoc />
        public override bool Scrolls
        {
            get { return false; }
        }

        #endregion

        #region Private-Members

        private static readonly List<string> Files = new List<string> { "Worker log", "Error log" };
        private static readonly List<string> Tails = new List<string> { "100", "250", "500", "1000", "5000" };
        private ComboBox _File = new ComboBox();
        private ComboBox _Tail = new ComboBox();
        private ComboBox _Severity = new ComboBox();
        private CheckBox _Follow = new CheckBox();
        private TextBox _Text = new TextBox();
        private TextBlock _Path = new TextBlock();
        private DispatcherTimer? _Timer;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the page.
        /// </summary>
        /// <param name="session">Session.</param>
        /// <param name="window">Main window.</param>
        public LogsPage(DesktopSession session, MainWindow window) : base(session, window)
        {
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public override IReadOnlyList<Control> Actions()
        {
            return new List<Control>
            {
                Ui.Button("Refresh", () => _ = RefreshAsync()),
                Ui.CopyButton("Copy the log", () => _ = XenoClipboard.CopyAsync(Window, _Text.Text)),
                Ui.Button("Show Log Folder", () => Report(XenoAppLauncher.OpenUrl(ClientConnectionState.LogDirectory).Message))
            };
        }

        /// <inheritdoc />
        public override Control Build()
        {
            _File = new ComboBox { ItemsSource = Files, SelectedIndex = 0, MinWidth = 130 };
            _File.SelectionChanged += (sender, args) => _ = RefreshAsync();
            _Tail = new ComboBox { ItemsSource = Tails, SelectedIndex = 1, MinWidth = 90 };
            _Tail.SelectionChanged += (sender, args) => _ = RefreshAsync();
            _Severity = new ComboBox { ItemsSource = ClientLogReader.SeverityFilters, SelectedIndex = 0, MinWidth = 100 };
            _Severity.SelectionChanged += (sender, args) => _ = RefreshAsync();
            _Follow = new CheckBox { Content = "Follow", IsChecked = true, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            _Follow.IsCheckedChanged += (sender, args) => UpdateTimer();
            _Path = Ui.Muted(String.Empty);
            _Text = new TextBox
            {
                IsReadOnly = true,
                AcceptsReturn = true,
                FontFamily = Ui.Mono,
                FontSize = 12,
                TextWrapping = Avalonia.Media.TextWrapping.NoWrap
            };

            DockPanel root = new DockPanel();
            StackPanel top = Ui.Stack(6, Ui.Row(Label("Log"), _File, Label("Lines"), _Tail, Label("Severity"), _Severity, _Follow), _Path);
            top.Margin = new Thickness(0, 0, 0, 10);
            DockPanel.SetDock(top, Dock.Top);
            root.Children.Add(top);
            root.Children.Add(Ui.Adopt(_Text));
            UpdateTimer();
            return root;
        }

        /// <inheritdoc />
        public override async Task RefreshAsync()
        {
            await GuardAsync("Logs", async () =>
            {
                bool error = _File.SelectedIndex == 1;
                int tail = Int32.Parse(Tails[Math.Max(0, _Tail.SelectedIndex)], System.Globalization.CultureInfo.InvariantCulture);
                string severity = _Severity.SelectedItem as string ?? "all";
                List<string> lines = await Workspace.ReadLogAsync(tail, severity, error, None).ConfigureAwait(true);
                string file = error ? ClientConnectionState.ErrorLogFile : ClientConnectionState.LogFile;
                _Path.Text = file + "  (" + lines.Count + " line" + (lines.Count == 1 ? String.Empty : "s") + ")";
                string text = lines.Count == 0 ? "(no log lines yet; start the client to create the log)" : String.Join(Environment.NewLine, lines);
                if (!String.Equals(_Text.Text, text, StringComparison.Ordinal))
                {
                    _Text.Text = text;
                    _Text.CaretIndex = text.Length;
                }
            }).ConfigureAwait(true);
        }

        /// <inheritdoc />
        public override void OnDeactivated()
        {
            _Timer?.Stop();
        }

        #endregion

        #region Private-Methods

        private static TextBlock Label(string text)
        {
            return new TextBlock { Text = text, Foreground = XenoTheme.Current.Muted, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        }

        private void UpdateTimer()
        {
            if (_Timer == null)
            {
                _Timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                _Timer.Tick += (sender, args) => _ = RefreshAsync();
            }

            if (_Follow.IsChecked == true) _Timer.Start();
            else _Timer.Stop();
        }

        #endregion
    }
}

namespace Xeno.Desktop.Dialogs
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Layout;
    using Avalonia.Media;
    using Xeno.Client;
    using Xeno.Client.Hosting;
    using Xeno.Client.Presentation;
    using Xeno.Desktop.Common;
    using Xeno.Desktop.Controls;

    /// <summary>
    /// The Xeno Settings window: a sidebar of sections (appearance, web dashboard, local inference server, concurrency,
    /// request history, telemetry), the selected section's fields, and Save, Revert, and Close. Opened from the Settings
    /// command in the desktop app's menus and from the Xeno Agent tray menu, on Linux, macOS, and Windows. Edits
    /// client.json through <see cref="SettingsEditor"/>, the same editor the Settings page uses.
    /// </summary>
    public sealed class SettingsWindow : Window
    {
        #region Private-Members

        private readonly DesktopSession _Session;
        private readonly SettingsEditor _Editor;
        private readonly Dictionary<string, Button> _SectionButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
        private readonly ContentControl _Content = new ContentControl();
        private readonly StackPanel _Result = new StackPanel { Spacing = 8 };
        private Button? _Save;
        private Button? _Revert;
        private string _Current = SettingsEditor.AppearanceSection;
        private bool _CloseConfirmed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the window.
        /// </summary>
        /// <param name="session">Desktop session.</param>
        /// <exception cref="ArgumentNullException">session is null.</exception>
        public SettingsWindow(DesktopSession session)
        {
            _Session = session ?? throw new ArgumentNullException(nameof(session));
            _Editor = new SettingsEditor(session);
            _Editor.Changed += (sender, args) => UpdateButtons();
            Title = "Xeno Settings";
            Width = 900;
            Height = 640;
            MinWidth = 720;
            MinHeight = 480;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Icon = XenoAssets.LoadWindowIcon();
            XenoTheme.Changed += OnThemeChanged;
            Closed += (sender, args) => XenoTheme.Changed -= OnThemeChanged;
            Closing += OnClosing;
            BuildLayout();
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Show a section.
        /// </summary>
        /// <param name="section">Section name from <see cref="SettingsEditor.Sections"/>.</param>
        public void ShowSection(string section)
        {
            if (!_SectionButtons.ContainsKey(section)) return;
            _Current = section;
            XenoPalette palette = XenoTheme.Current;
            StackPanel body = new StackPanel { Spacing = 16, Margin = new Thickness(28, 24, 28, 24) };
            body.Children.Add(new TextBlock { Text = section, FontSize = 22, FontWeight = FontWeight.SemiBold, Foreground = palette.Text });
            string description = SettingsEditor.Describe(section);
            if (!String.IsNullOrWhiteSpace(description)) body.Children.Add(Ui.Muted(description));
            body.Children.Add(_Result.Parent == null ? _Result : Detach(_Result));
            body.Children.Add(Ui.Card(null, _Editor.Section(section)));
            _Content.Content = new ScrollViewer { Content = body, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
            foreach (KeyValuePair<string, Button> pair in _SectionButtons)
            {
                bool active = String.Equals(pair.Key, section, StringComparison.Ordinal);
                pair.Value.Background = active ? palette.SidebarActive : Brushes.Transparent;
                pair.Value.Foreground = active ? palette.SidebarActiveText : palette.SidebarText;
            }
        }

        #endregion

        #region Private-Methods

        private void BuildLayout()
        {
            XenoPalette palette = XenoTheme.Current;
            Background = palette.Background;
            _SectionButtons.Clear();

            StackPanel nav = new StackPanel { Spacing = 2, Margin = new Thickness(10, 4) };
            nav.Children.Add(new TextBlock
            {
                Text = "SETTINGS",
                FontSize = 10.5,
                FontWeight = FontWeight.SemiBold,
                Foreground = palette.SidebarMuted,
                Margin = new Thickness(10, 12, 0, 4)
            });

            foreach (string section in _Editor.Sections)
            {
                string target = section;
                Button button = new Button
                {
                    Content = section,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Padding = new Thickness(12, 7),
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Foreground = palette.SidebarText
                };
                ToolTip.SetTip(button, SettingsEditor.Describe(section));
                button.Click += (sender, args) => ShowSection(target);
                _SectionButtons[target] = button;
                nav.Children.Add(button);
            }

            DockPanel sidebar = new DockPanel { Width = 220 };
            StackPanel brand = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(18, 18, 18, 10) };
            Avalonia.Media.Imaging.Bitmap? logo = XenoAssets.LoadLogo();
            if (logo != null) brand.Children.Add(new Image { Source = logo, Width = 26, Height = 26 });
            brand.Children.Add(new TextBlock { Text = "Settings", FontSize = 18, FontWeight = FontWeight.SemiBold, Foreground = XenoTheme.Current.SidebarText, VerticalAlignment = VerticalAlignment.Center });
            DockPanel.SetDock(brand, Dock.Top);
            sidebar.Children.Add(brand);
            TextBlock file = new TextBlock
            {
                Text = ClientSettings.SettingsFile,
                FontSize = 11,
                Foreground = palette.SidebarMuted,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(18, 10, 18, 16)
            };
            ToolTip.SetTip(file, "Settings file");
            DockPanel.SetDock(file, Dock.Bottom);
            sidebar.Children.Add(file);
            sidebar.Children.Add(new ScrollViewer { Content = nav });

            _Save = Ui.Primary("Save", () => _ = SaveAsync());
            _Revert = Ui.Button("Revert", Revert, "Discard unsaved changes.");
            Button close = Ui.Button("Close", Close);
            Button openFile = Ui.Button("Open client.json", () => _Session.Report(XenoAppLauncher.OpenUrl(ClientSettings.SettingsFile).Message), "Open the settings file in your editor.");
            DockPanel footer = new DockPanel { LastChildFill = false, Margin = new Thickness(20, 12) };
            StackPanel right = Ui.Row(_Revert, close, _Save);
            DockPanel.SetDock(right, Dock.Right);
            DockPanel.SetDock(openFile, Dock.Left);
            footer.Children.Add(right);
            footer.Children.Add(openFile);
            Border footerBar = new Border { BorderBrush = palette.Border, BorderThickness = new Thickness(0, 1, 0, 0), Background = palette.Panel, Child = footer };

            DockPanel main = new DockPanel();
            DockPanel.SetDock(footerBar, Dock.Bottom);
            main.Children.Add(footerBar);
            main.Children.Add(_Content);

            DockPanel root = new DockPanel();
            Border sidebarBorder = new Border { Background = palette.Sidebar, Child = sidebar };
            DockPanel.SetDock(sidebarBorder, Dock.Left);
            root.Children.Add(sidebarBorder);
            root.Children.Add(main);
            Content = root;
            ShowSection(_Current);
            UpdateButtons();
        }

        private void OnThemeChanged(object? sender, EventArgs args)
        {
            BuildLayout();
        }

        private void UpdateButtons()
        {
            bool dirty = _Editor.IsDirty;
            if (_Save != null) _Save.IsEnabled = dirty;
            if (_Revert != null) _Revert.IsEnabled = dirty;
            Title = dirty ? "Xeno Settings (unsaved changes)" : "Xeno Settings";
        }

        private void Revert()
        {
            _Editor.Reload();
            _Result.Children.Clear();
            ShowSection(_Current);
        }

        private async Task<bool> SaveAsync()
        {
            _Result.Children.Clear();
            SettingsSaveOutcome outcome = await _Editor.SaveAsync(CancellationToken.None).ConfigureAwait(true);
            _Result.Children.Add(Ui.Notice(outcome.Message, outcome.Tone));
            if (outcome.RestartSuggested)
            {
                _Result.Children.Add(Ui.Row(Ui.Primary("Restart Client", () =>
                {
                    if (Application.Current is IXenoMenuHost host) host.Execute(XenoCommandEnum.RestartClient);
                    _Result.Children.Clear();
                    _Result.Children.Add(Ui.Notice("Restarting the client...", XenoToneEnum.Info));
                })));
            }

            UpdateButtons();
            return outcome.Saved;
        }

        private async void OnClosing(object? sender, WindowClosingEventArgs args)
        {
            if (_CloseConfirmed || !_Editor.IsDirty) return;
            args.Cancel = true;
            bool discard = await XenoDialogs.ConfirmAsync(this, "Unsaved changes", "Close Settings and discard your unsaved changes?", "Discard").ConfigureAwait(true);
            if (!discard) return;
            _CloseConfirmed = true;
            Close();
        }

        private static Control Detach(Control control)
        {
            if (control.Parent is Panel parent) parent.Children.Remove(control);
            return control;
        }

        #endregion
    }
}

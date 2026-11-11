namespace Xeno.Desktop.Pages
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Layout;
    using Xeno.Client;
    using Xeno.Client.Hosting;
    using Xeno.Client.Presentation;
    using Xeno.Desktop.Common;
    using Xeno.Desktop.Controls;

    /// <summary>
    /// Settings page: every section of the shared <see cref="SettingsEditor"/> on one scrolling page, saved to client.json.
    /// The Settings window (<see cref="Dialogs.SettingsWindow"/>) edits the same fields one section at a time.
    /// </summary>
    public sealed class SettingsPage : DesktopPage
    {
        #region Public-Members

        /// <inheritdoc />
        public override XenoPageEnum Page
        {
            get { return XenoPageEnum.Settings; }
        }

        #endregion

        #region Private-Members

        private SettingsEditor? _Editor;
        private StackPanel _Result = new StackPanel();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the page.
        /// </summary>
        /// <param name="session">Session.</param>
        /// <param name="window">Main window.</param>
        public SettingsPage(DesktopSession session, MainWindow window) : base(session, window)
        {
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public override IReadOnlyList<Control> Actions()
        {
            return new List<Control>
            {
                Ui.Button("Open Settings Window", () =>
                {
                    if (Application.Current is IXenoMenuHost host) host.Execute(XenoCommandEnum.Settings);
                }),
                Ui.Button("Open client.json", () => Report(XenoAppLauncher.OpenUrl(ClientSettings.SettingsFile).Message)),
                Ui.Button("Revert", () => Window.Navigate(XenoPageEnum.Settings)),
                Ui.Primary("Save", () => _ = SaveAsync())
            };
        }

        /// <inheritdoc />
        public override Control Build()
        {
            _Editor = new SettingsEditor(Session);
            _Result = new StackPanel { Spacing = 8 };
            StackPanel stack = new StackPanel { Spacing = 16 };
            stack.Children.Add(Ui.Adopt(_Result));
            foreach (string section in _Editor.Sections)
            {
                stack.Children.Add(Ui.Card(section, Ui.Stack(12, Ui.Muted(SettingsEditor.Describe(section)), _Editor.Section(section))));
            }

            stack.Children.Add(Ui.Muted("Settings file: " + ClientSettings.SettingsFile));
            return stack;
        }

        #endregion

        #region Private-Methods

        private async Task SaveAsync()
        {
            if (_Editor == null) return;
            _Result.Children.Clear();
            SettingsSaveOutcome outcome = await _Editor.SaveAsync(None).ConfigureAwait(true);
            _Result.Children.Add(Ui.Notice(outcome.Message, outcome.Tone));
            if (outcome.RestartSuggested)
            {
                _Result.Children.Add(Ui.Row(Ui.Primary("Restart Client", () =>
                {
                    if (Application.Current is IXenoMenuHost host) host.Execute(XenoCommandEnum.RestartClient);
                })));
            }
        }

        #endregion
    }
}

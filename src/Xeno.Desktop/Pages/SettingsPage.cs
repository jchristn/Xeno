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

    /// <summary>
    /// Settings: appearance, plus every local setting from the shared <see cref="XenoSettingsCatalog"/> (web dashboard,
    /// local inference server, request history, telemetry), saved to client.json.
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

        private readonly Dictionary<string, Control> _Inputs = new Dictionary<string, Control>(StringComparer.Ordinal);
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
                Ui.Button("Open client.json", () => Report(XenoAppLauncher.OpenUrl(ClientSettings.SettingsFile).Message)),
                Ui.Button("Revert", () => Window.Navigate(XenoPageEnum.Settings)),
                Ui.Primary("Save", () => _ = SaveAsync())
            };
        }

        /// <inheritdoc />
        public override Control Build()
        {
            _Inputs.Clear();
            _Result = new StackPanel { Spacing = 8 };
            Dictionary<string, string> values = XenoSettingsCatalog.Read(Workspace.LoadSettings());
            StackPanel stack = new StackPanel { Spacing = 16 };
            stack.Children.Add(_Result);
            stack.Children.Add(Ui.Card("Appearance", BuildAppearance()));
            foreach (string section in XenoSettingsCatalog.Sections)
            {
                StackPanel fields = new StackPanel { Spacing = 12 };
                foreach (XenoSettingField field in XenoSettingsCatalog.Fields.Where(item => item.Section == section))
                {
                    Control input = BuildInput(field, values.TryGetValue(field.Key, out string? value) ? value : String.Empty);
                    _Inputs[field.Key] = input;
                    if (field.Kind == XenoSettingKindEnum.Toggle)
                    {
                        fields.Children.Add(String.IsNullOrWhiteSpace(field.Help) ? input : Ui.Stack(2, input, Ui.Muted(field.Help)));
                    }
                    else
                    {
                        input.MaxWidth = 360;
                        input.HorizontalAlignment = HorizontalAlignment.Left;
                        fields.Children.Add(Ui.Field(field.Label, input, field.Help));
                    }
                }

                stack.Children.Add(Ui.Card(section, fields));
            }

            stack.Children.Add(Ui.Muted("Settings file: " + ClientSettings.SettingsFile));
            return stack;
        }

        #endregion

        #region Private-Methods

        private Control BuildAppearance()
        {
            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
            foreach (XenoAppearanceEnum appearance in Enum.GetValues<XenoAppearanceEnum>())
            {
                XenoAppearanceEnum value = appearance;
                RadioButton radio = new RadioButton { Content = appearance.ToString(), GroupName = "appearance", IsChecked = XenoTheme.Appearance == appearance };
                radio.IsCheckedChanged += (sender, args) =>
                {
                    if (radio.IsChecked != true || XenoTheme.Appearance == value) return;
                    XenoCommandEnum command = value == XenoAppearanceEnum.Light ? XenoCommandEnum.AppearanceLight : value == XenoAppearanceEnum.Dark ? XenoCommandEnum.AppearanceDark : XenoCommandEnum.AppearanceSystem;
                    if (Application.Current is IXenoMenuHost host) host.Execute(command);
                };
                row.Children.Add(radio);
            }

            return row;
        }

        private static Control BuildInput(XenoSettingField field, string value)
        {
            switch (field.Kind)
            {
                case XenoSettingKindEnum.Toggle:
                    return new CheckBox { Content = field.Label, IsChecked = value == "true" };
                case XenoSettingKindEnum.Choice:
                    List<string> choices = field.Choices.ToList();
                    if (!choices.Contains(value)) choices.Add(value);
                    return new ComboBox { ItemsSource = choices, SelectedItem = value, MinWidth = 200 };
                default:
                    return new TextBox { Text = value, MinWidth = 200 };
            }
        }

        private Dictionary<string, string> Collect()
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, Control> pair in _Inputs)
            {
                if (pair.Value is CheckBox check) values[pair.Key] = check.IsChecked == true ? "true" : "false";
                else if (pair.Value is ComboBox combo) values[pair.Key] = combo.SelectedItem as string ?? String.Empty;
                else if (pair.Value is TextBox box) values[pair.Key] = box.Text ?? String.Empty;
            }

            return values;
        }

        private async Task SaveAsync()
        {
            _Result.Children.Clear();
            try
            {
                bool restart = await Workspace.UpdateSettingsAsync(Collect(), None).ConfigureAwait(true);
                bool running = Session.Status?.Running == true;
                if (restart && running)
                {
                    _Result.Children.Add(Ui.Notice("Saved. Restart the client to apply listener and telemetry changes.", XenoToneEnum.Warning));
                    _Result.Children.Add(Ui.Row(Ui.Primary("Restart Client", () =>
                    {
                        if (Application.Current is IXenoMenuHost host) host.Execute(XenoCommandEnum.RestartClient);
                    })));
                }
                else
                {
                    _Result.Children.Add(Ui.Notice("Saved.", XenoToneEnum.Success));
                }

                Report("Saved settings to " + ClientSettings.SettingsFile + ".");
            }
            catch (ArgumentException ex)
            {
                _Result.Children.Add(Ui.Notice(ex.Message, XenoToneEnum.Danger));
            }
        }

        #endregion
    }
}

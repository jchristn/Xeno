namespace Xeno.Desktop.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Avalonia;
    using Avalonia.Automation;
    using Avalonia.Controls;
    using Avalonia.Layout;
    using Xeno.Client;
    using Xeno.Client.Presentation;
    using Xeno.Desktop.Common;

    /// <summary>
    /// Editable controls for every setting in the shared <see cref="XenoSettingsCatalog"/>, plus the desktop appearance.
    /// Both the Settings page and the Settings window use it, so they edit the same fields the same way. Each section's
    /// panel is built once and kept, so edits survive switching sections. Not thread safe; use it on the UI thread.
    /// </summary>
    public sealed class SettingsEditor
    {
        #region Public-Members

        /// <summary>
        /// Name of the appearance section, which is stored in the front-end preferences rather than client.json.
        /// </summary>
        public const string AppearanceSection = "Appearance";

        /// <summary>
        /// Sections in display order: appearance first, then the catalog sections.
        /// </summary>
        public IReadOnlyList<string> Sections
        {
            get
            {
                List<string> sections = new List<string> { AppearanceSection };
                sections.AddRange(XenoSettingsCatalog.Sections);
                return sections;
            }
        }

        /// <summary>
        /// True when a client.json value differs from what was loaded.
        /// </summary>
        public bool IsDirty
        {
            get
            {
                Dictionary<string, string> current = Collect();
                return current.Any(pair => !_Loaded.TryGetValue(pair.Key, out string? before) || !String.Equals(before, pair.Value, StringComparison.Ordinal));
            }
        }

        /// <summary>
        /// Raised when any value changes.
        /// </summary>
        public event EventHandler? Changed;

        #endregion

        #region Private-Members

        private readonly DesktopSession _Session;
        private readonly Dictionary<string, Control> _Inputs = new Dictionary<string, Control>(StringComparer.Ordinal);
        private readonly Dictionary<string, Control> _Panels = new Dictionary<string, Control>(StringComparer.Ordinal);
        private Dictionary<string, string> _Loaded = new Dictionary<string, string>(StringComparer.Ordinal);

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the editor and load the current settings.
        /// </summary>
        /// <param name="session">Desktop session.</param>
        /// <exception cref="ArgumentNullException">session is null.</exception>
        public SettingsEditor(DesktopSession session)
        {
            _Session = session ?? throw new ArgumentNullException(nameof(session));
            Reload();
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Discard edits and load the settings from client.json again. Section panels are rebuilt on next use.
        /// </summary>
        public void Reload()
        {
            _Loaded = XenoSettingsCatalog.Read(_Session.Workspace.LoadSettings());
            _Inputs.Clear();
            _Panels.Clear();
            foreach (XenoSettingField field in XenoSettingsCatalog.Fields)
            {
                Control input = BuildInput(field, _Loaded.TryGetValue(field.Key, out string? value) ? value : String.Empty);
                _Inputs[field.Key] = input;
            }

            Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// The panel of fields for one section. The same control is returned each time until <see cref="Reload"/>.
        /// </summary>
        /// <param name="section">Section from <see cref="Sections"/>.</param>
        /// <returns>Panel.</returns>
        public Control Section(string section)
        {
            // The appearance panel is rebuilt every time, because a scheme change redraws the window with new colors.
            if (String.Equals(section, AppearanceSection, StringComparison.Ordinal)) return BuildAppearance();
            if (_Panels.TryGetValue(section, out Control? existing))
            {
                if (existing.Parent is Panel parent) parent.Children.Remove(existing);
                else if (existing.Parent is Decorator decorator) decorator.Child = null;
                else if (existing.Parent is ContentControl content) content.Content = null;
                return existing;
            }

            Control panel = String.Equals(section, AppearanceSection, StringComparison.Ordinal) ? BuildAppearance() : BuildFields(section);
            _Panels[section] = panel;
            return panel;
        }

        /// <summary>
        /// One-line description of a section.
        /// </summary>
        /// <param name="section">Section.</param>
        /// <returns>Description, never null.</returns>
        public static string Describe(string section)
        {
            if (String.Equals(section, AppearanceSection, StringComparison.Ordinal)) return "How the Xeno desktop app and Xeno Agent look: light or dark, and the colors. Applies right away and is saved on this computer.";
            return XenoSettingsCatalog.SectionDescription(section);
        }

        /// <summary>
        /// Validate and save the edited values to client.json.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Outcome.</returns>
        public async Task<SettingsSaveOutcome> SaveAsync(CancellationToken token)
        {
            try
            {
                bool restart = await _Session.Workspace.UpdateSettingsAsync(Collect(), token).ConfigureAwait(true);
                _Loaded = Collect();
                Changed?.Invoke(this, EventArgs.Empty);
                bool running = _Session.Status?.Running == true;
                _Session.Report("Saved settings to " + ClientSettings.SettingsFile + ".");
                if (restart && running)
                {
                    return new SettingsSaveOutcome { Saved = true, RestartSuggested = true, Message = "Saved. Restart the client to apply listener and telemetry changes.", Tone = XenoToneEnum.Warning };
                }

                return new SettingsSaveOutcome { Saved = true, Message = "Saved.", Tone = XenoToneEnum.Success };
            }
            catch (ArgumentException ex)
            {
                return new SettingsSaveOutcome { Saved = false, Message = ex.Message, Tone = XenoToneEnum.Danger };
            }
        }

        #endregion

        #region Private-Methods

        private Control BuildFields(string section)
        {
            StackPanel fields = new StackPanel { Spacing = 14 };
            foreach (XenoSettingField field in XenoSettingsCatalog.Fields.Where(item => item.Section == section))
            {
                Control input = _Inputs[field.Key];
                if (field.Kind == XenoSettingKindEnum.Toggle)
                {
                    fields.Children.Add(String.IsNullOrWhiteSpace(field.Help) ? input : Ui.Stack(2, input, Indent(Ui.Muted(field.Help))));
                }
                else
                {
                    input.MaxWidth = 360;
                    input.HorizontalAlignment = HorizontalAlignment.Left;
                    fields.Children.Add(Ui.Field(field.Label, input, field.Help));
                }
            }

            return fields;
        }

        private static Control Indent(Control control)
        {
            control.Margin = new Thickness(28, 0, 0, 0);
            return control;
        }

        private Control BuildAppearance()
        {
            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
            foreach (XenoAppearanceEnum appearance in Enum.GetValues<XenoAppearanceEnum>())
            {
                XenoAppearanceEnum value = appearance;
                RadioButton radio = new RadioButton { Content = appearance.ToString(), GroupName = "settings-appearance-" + GetHashCode(), IsChecked = XenoTheme.Appearance == appearance };
                radio.IsCheckedChanged += (sender, args) =>
                {
                    if (radio.IsChecked != true || XenoTheme.Appearance == value) return;
                    XenoCommandEnum command = value == XenoAppearanceEnum.Light ? XenoCommandEnum.AppearanceLight : value == XenoAppearanceEnum.Dark ? XenoCommandEnum.AppearanceDark : XenoCommandEnum.AppearanceSystem;
                    if (Application.Current is IXenoMenuHost host) host.Execute(command);
                };
                row.Children.Add(radio);
            }

            StackPanel stack = new StackPanel { Spacing = 16 };
            stack.Children.Add(Ui.Field("Theme", row, "System follows your operating system's light or dark setting."));
            stack.Children.Add(Ui.Field("Colors", BuildSchemeTiles(), "Pick a palette, or Custom to choose your own colors."));
            if (XenoTheme.Scheme.Id == XenoColorSchemes.CustomId) stack.Children.Add(BuildCustomEditor());
            return stack;
        }

        private Control BuildSchemeTiles()
        {
            XenoPalette palette = XenoTheme.Current;
            bool dark = palette.IsDark;
            WrapPanel tiles = new WrapPanel { Orientation = Orientation.Horizontal };
            List<XenoColorScheme> schemes = XenoColorSchemes.BuiltIn.ToList();
            schemes.Add(_Session.Preferences.DesktopCustomColors ?? XenoColorSchemes.CreateCustom(XenoTheme.Scheme));
            foreach (XenoColorScheme scheme in schemes)
            {
                string id = scheme.Id == XenoColorSchemes.CustomId ? XenoColorSchemes.CustomId : scheme.Id;
                XenoResolvedColors colors = XenoColorSchemes.Resolve(scheme, dark);
                bool selected = String.Equals(XenoTheme.Scheme.Id, id, StringComparison.OrdinalIgnoreCase);
                StackPanel chips = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 0 };
                foreach (string hex in new[] { colors.Sidebar, colors.SidebarActive, colors.AccentFill, colors.Background })
                {
                    chips.Children.Add(new Border { Width = 28, Height = 18, Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(hex)) });
                }

                Border strip = new Border { CornerRadius = new CornerRadius(4), ClipToBounds = true, BorderBrush = palette.Border, BorderThickness = new Thickness(1), Child = chips, HorizontalAlignment = HorizontalAlignment.Left };
                string name = id == XenoColorSchemes.CustomId ? "Custom" : scheme.Name;
                Button tile = new Button
                {
                    Width = 150,
                    Margin = new Thickness(0, 0, 8, 8),
                    Padding = new Thickness(8),
                    Background = palette.Panel,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    BorderBrush = selected ? palette.Accent : palette.Border,
                    BorderThickness = new Thickness(selected ? 2 : 1),
                    Content = Ui.Stack(6, strip, new TextBlock { Text = name + (selected ? "  \u2713" : String.Empty), FontWeight = selected ? Avalonia.Media.FontWeight.SemiBold : Avalonia.Media.FontWeight.Normal })
                };
                ToolTip.SetTip(tile, id == XenoColorSchemes.CustomId ? "Your own colors." : scheme.Description);
                AutomationProperties.SetName(tile, name + " colors");
                tile.Click += (sender, args) => SelectScheme(id);
                tiles.Children.Add(tile);
            }

            return tiles;
        }

        private void SelectScheme(string id)
        {
            XenoFrontEndPreferences preferences = _Session.Preferences;
            if (id == XenoColorSchemes.CustomId && preferences.DesktopCustomColors == null)
            {
                preferences.DesktopCustomColors = XenoColorSchemes.CreateCustom(XenoTheme.Scheme);
            }

            preferences.DesktopColorScheme = id;
            _Session.SavePreferences();
            XenoTheme.SetScheme(XenoColorSchemes.Selected(preferences));
        }

        private Control BuildCustomEditor()
        {
            XenoFrontEndPreferences preferences = _Session.Preferences;
            XenoColorScheme custom = preferences.DesktopCustomColors ?? XenoColorSchemes.CreateCustom(XenoTheme.Scheme);
            preferences.DesktopCustomColors = custom;

            ComboBox start = new ComboBox { ItemsSource = XenoColorSchemes.BuiltIn.Select(item => item.Name).ToList(), SelectedIndex = 0, MinWidth = 160 };
            Button copy = Ui.Button("Copy Colors", () =>
            {
                XenoColorScheme from = XenoColorSchemes.BuiltIn[Math.Clamp(start.SelectedIndex, 0, XenoColorSchemes.BuiltIn.Count - 1)];
                preferences.DesktopCustomColors = XenoColorSchemes.CreateCustom(from);
                _Session.SavePreferences();
                XenoTheme.SetScheme(XenoColorSchemes.Selected(preferences));
            }, "Replace your custom colors with a palette's colors, then adjust them.");

            Grid grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto"), ColumnSpacing = 20, RowSpacing = 8 };
            AddCell(grid, new TextBlock { Text = "Color", FontWeight = Avalonia.Media.FontWeight.SemiBold }, 0, 0);
            AddCell(grid, new TextBlock { Text = "Light", FontWeight = Avalonia.Media.FontWeight.SemiBold }, 0, 1);
            AddCell(grid, new TextBlock { Text = "Dark", FontWeight = Avalonia.Media.FontWeight.SemiBold }, 0, 2);
            string[] labels = { "Background", "Panels", "Text", "Accent", "Sidebar", "Sidebar highlight" };
            for (int i = 0; i < labels.Length; i++)
            {
                int index = i;
                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                AddCell(grid, new TextBlock { Text = labels[i], VerticalAlignment = VerticalAlignment.Center }, i + 1, 0);
                AddCell(grid, new ColorField(Get(custom.Light, index), "Light " + labels[i].ToLowerInvariant(), hex => UpdateCustom(false, index, hex)), i + 1, 1);
                AddCell(grid, new ColorField(Get(custom.Dark, index), "Dark " + labels[i].ToLowerInvariant(), hex => UpdateCustom(true, index, hex)), i + 1, 2);
            }

            grid.RowDefinitions.Insert(0, new RowDefinition(GridLength.Auto));
            StackPanel editor = Ui.Stack(12,
                Ui.Muted("Other colors, such as secondary text, borders, and button text, are worked out from these so they stay readable."),
                Ui.Row(new TextBlock { Text = "Start from", VerticalAlignment = VerticalAlignment.Center }, start, copy),
                grid);
            foreach (string problem in XenoColorSchemes.Check(custom.Light, false)) editor.Children.Add(Ui.Notice("Light: " + problem, XenoToneEnum.Warning));
            foreach (string problem in XenoColorSchemes.Check(custom.Dark, true)) editor.Children.Add(Ui.Notice("Dark: " + problem, XenoToneEnum.Warning));
            return Ui.Field("Custom colors", editor);
        }

        private void UpdateCustom(bool dark, int index, string hex)
        {
            XenoFrontEndPreferences preferences = _Session.Preferences;
            XenoColorScheme custom = preferences.DesktopCustomColors ?? XenoColorSchemes.CreateCustom(XenoTheme.Scheme);
            preferences.DesktopCustomColors = custom;
            XenoColorSet set = dark ? custom.Dark : custom.Light;
            switch (index)
            {
                case 0: set.Background = hex; break;
                case 1: set.Panel = hex; break;
                case 2: set.Text = hex; break;
                case 3: set.Accent = hex; break;
                case 4: set.Sidebar = hex; break;
                default: set.SidebarActive = hex; break;
            }

            preferences.DesktopColorScheme = XenoColorSchemes.CustomId;
            _Session.SavePreferences();
            XenoTheme.SetScheme(XenoColorSchemes.Selected(preferences));
        }

        private static string Get(XenoColorSet set, int index)
        {
            switch (index)
            {
                case 0: return set.Background;
                case 1: return set.Panel;
                case 2: return set.Text;
                case 3: return set.Accent;
                case 4: return set.Sidebar;
                default: return set.SidebarActive;
            }
        }

        private static void AddCell(Grid grid, Control control, int row, int column)
        {
            Grid.SetRow(control, row);
            Grid.SetColumn(control, column);
            grid.Children.Add(control);
        }

        private Control BuildInput(XenoSettingField field, string value)
        {
            switch (field.Kind)
            {
                case XenoSettingKindEnum.Toggle:
                    CheckBox check = new CheckBox { Content = field.Label, IsChecked = value == "true" };
                    check.IsCheckedChanged += (sender, args) => Changed?.Invoke(this, EventArgs.Empty);
                    return check;
                case XenoSettingKindEnum.Choice:
                    List<string> choices = field.Choices.ToList();
                    if (!choices.Contains(value)) choices.Add(value);
                    ComboBox combo = new ComboBox { ItemsSource = choices, SelectedItem = value, MinWidth = 220 };
                    combo.SelectionChanged += (sender, args) => Changed?.Invoke(this, EventArgs.Empty);
                    return combo;
                default:
                    TextBox box = new TextBox { Text = value, MinWidth = 220 };
                    box.TextChanged += (sender, args) => Changed?.Invoke(this, EventArgs.Empty);
                    return box;
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

        #endregion
    }
}

namespace Xeno.Tui.Pages
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Layout;
    using TUIKit.Modals;
    using TUIKit.Widgets;
    using Xeno.Client;
    using Xeno.Client.Presentation;

    /// <summary>
    /// Settings: appearance and every local setting from the shared <see cref="XenoSettingsCatalog"/>. Ctrl+S saves.
    /// </summary>
    internal sealed class SettingsPage : TuiPage
    {
        #region Public-Members

        /// <inheritdoc />
        public override XenoPageEnum Page
        {
            get { return XenoPageEnum.Settings; }
        }

        /// <inheritdoc />
        public override IWidget Root
        {
            get { return _Root; }
        }

        /// <inheritdoc />
        public override IReadOnlyList<KeyHint> Hints
        {
            get { return new List<KeyHint> { new KeyHint("Ctrl+S", "Save") }; }
        }

        #endregion

        #region Private-Members

        private const int FormWidth = 96;
        private readonly FramedStack _Root = new FramedStack(SplitOrientation.Vertical);
        private readonly Form _Form = new Form { WrapFocus = false };
        private readonly ScrollView _Scroll;
        private readonly ButtonRow _Actions = new ButtonRow();
        private readonly Dropdown<string> _Appearance = new Dropdown<string>(Enum.GetNames(typeof(XenoAppearanceEnum)));
        private readonly Dropdown<string> _Colors = new Dropdown<string>(XenoColorSchemes.BuiltIn.Select(item => item.Name).Concat(new[] { "Custom" }).ToList());
        private readonly Dictionary<string, IWidget> _Inputs = new Dictionary<string, IWidget>(StringComparer.Ordinal);

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the page.
        /// </summary>
        /// <param name="context">Context.</param>
        public SettingsPage(TuiContext context) : base(context)
        {
            _Appearance.SelectedIndex = (int)context.Preferences.TerminalAppearance;
            _Appearance.SelectionChanged += (sender, args) =>
            {
                XenoAppearanceEnum value = (XenoAppearanceEnum)Math.Max(0, _Appearance.SelectedIndex);
                Context.Execute(value == XenoAppearanceEnum.Light ? XenoCommandEnum.AppearanceLight : value == XenoAppearanceEnum.Dark ? XenoCommandEnum.AppearanceDark : XenoCommandEnum.AppearanceSystem);
            };
            _Form.Add("Appearance", _Appearance);

            // The same palettes as the desktop app; Custom uses the custom colors shared with it.
            string scheme = context.Preferences.TerminalColorScheme;
            int schemeIndex = String.Equals(scheme, XenoColorSchemes.CustomId, StringComparison.OrdinalIgnoreCase)
                ? XenoColorSchemes.BuiltIn.Count
                : Math.Max(0, XenoColorSchemes.BuiltIn.ToList().FindIndex(item => String.Equals(item.Id, scheme, StringComparison.OrdinalIgnoreCase)));
            _Colors.SelectedIndex = schemeIndex;
            _Colors.SelectionChanged += (sender, args) =>
            {
                int index = Math.Max(0, _Colors.SelectedIndex);
                if (index >= XenoColorSchemes.BuiltIn.Count)
                {
                    if (Context.Preferences.DesktopCustomColors == null) Context.Preferences.DesktopCustomColors = XenoColorSchemes.CreateCustom(XenoColorSchemes.SelectedForTerminal(Context.Preferences));
                    Context.Preferences.TerminalColorScheme = XenoColorSchemes.CustomId;
                }
                else
                {
                    Context.Preferences.TerminalColorScheme = XenoColorSchemes.BuiltIn[index].Id;
                }

                Context.SavePreferences();
                Context.Restyle();
            };
            _Form.Add("Colors", _Colors);
            Dictionary<string, string> values = XenoSettingsCatalog.Read(Workspace.LoadSettings());
            string section = String.Empty;
            foreach (XenoSettingField field in XenoSettingsCatalog.Fields)
            {
                if (!String.Equals(field.Section, section, StringComparison.Ordinal))
                {
                    section = field.Section;
                    _Form.AddSection(section);
                }

                string label = field.Label;
                string value = values.TryGetValue(field.Key, out string? current) ? current : String.Empty;
                IWidget input;
                if (field.Kind == XenoSettingKindEnum.Toggle)
                {
                    input = _Form.AddCheckbox(label, value == "true");
                }
                else if (field.Kind == XenoSettingKindEnum.Choice)
                {
                    List<string> choices = field.Choices.ToList();
                    if (!choices.Contains(value)) choices.Add(value);
                    Dropdown<string> dropdown = new Dropdown<string>(choices) { SelectedIndex = Math.Max(0, choices.IndexOf(value)) };
                    input = _Form.Add(label, dropdown);
                }
                else
                {
                    input = _Form.Add(label, new TextField { Value = value });
                }

                _Inputs[field.Key] = input;
                if (!String.IsNullOrWhiteSpace(field.Help)) _Form.SetFieldHint(_Form.FieldCount - 1, field.Help);
            }

            // Lay the form out once at the scroll width so wrapped help text counts toward the scrollable height.
            _Form.Render(new BufferSurface(new CellBuffer(FormWidth, 1000)));
            _Scroll = new ScrollView(_Form, FormWidth, Math.Max(1, _Form.ContentHeight)) { ForwardKeys = true, ShowHorizontalScrollbar = false };
            _Actions.Add("Save", () => _ = SaveAsync());
            _Actions.Add("Revert", Revert);
            _Actions.Add("Custom Colors", () => _ = EditCustomColorsAsync());
            Frame(_Root, _Scroll, StackSize.Weighted(1, 4), "Settings (client.json)");
            Frame(_Root, _Actions, StackSize.Fixed(1), "Actions");
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public override Task RefreshAsync()
        {
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public override bool HandleKey(KeyEvent key)
        {
            if (key.Code == KeyCode.Character && (key.Modifiers & KeyModifiers.Ctrl) != 0 && (key.Rune == 's' || key.Rune == 'S'))
            {
                _ = SaveAsync();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Current editor values, for tests.
        /// </summary>
        /// <returns>Values by setting key.</returns>
        public Dictionary<string, string> Collect()
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, IWidget> pair in _Inputs)
            {
                if (pair.Value is Checkbox check) values[pair.Key] = check.Checked ? "true" : "false";
                else if (pair.Value is Dropdown<string> dropdown) values[pair.Key] = dropdown.SelectedItem ?? String.Empty;
                else if (pair.Value is TextField text) values[pair.Key] = text.Value;
            }

            return values;
        }

        /// <summary>
        /// Set an editor value, for tests.
        /// </summary>
        /// <param name="key">Setting key.</param>
        /// <param name="value">Value.</param>
        public void SetValue(string key, string value)
        {
            if (!_Inputs.TryGetValue(key, out IWidget? input)) return;
            if (input is Checkbox check) check.Checked = value == "true";
            else if (input is Dropdown<string> dropdown) dropdown.SelectedIndex = dropdown.Items.ToList().IndexOf(value);
            else if (input is TextField text) text.Value = value;
        }

        #endregion

        #region Private-Methods

        private Task SaveAsync()
        {
            Dictionary<string, string> values = Collect();
            bool running = Context.Status?.Running == true;
            return Context.LoadAsync(token => Workspace.UpdateSettingsAsync(values, token), restart =>
            {
                if (restart && running) Context.Notify("Saved. Restart the client (Client > Restart Client) to apply listener and telemetry changes.", NotificationSeverity.Warning);
                else Context.Notify("Saved settings to " + ClientSettings.SettingsFile + ".", NotificationSeverity.Success);
            });
        }

        private void Revert()
        {
            Dictionary<string, string> values = XenoSettingsCatalog.Read(Workspace.LoadSettings());
            foreach (KeyValuePair<string, string> pair in values) SetValue(pair.Key, pair.Value);
            Context.Notify("Reverted to the saved settings.", NotificationSeverity.Info);
        }

        private static readonly string[] ColorLabels = { "Background", "Panels", "Text", "Accent", "Sidebar", "Sidebar highlight" };

        private async Task EditCustomColorsAsync()
        {
            // Six colors for light terminals and six for dark ones, as hex; the rest are worked out from them.
            XenoColorScheme custom = Context.Preferences.DesktopCustomColors ?? XenoColorSchemes.CreateCustom(XenoColorSchemes.SelectedForTerminal(Context.Preferences));
            List<XenoFormFieldSpec> fields = new List<XenoFormFieldSpec>();
            foreach (bool dark in new[] { false, true })
            {
                XenoColorSet set = dark ? custom.Dark : custom.Light;
                string[] values = { set.Background, set.Panel, set.Text, set.Accent, set.Sidebar, set.SidebarActive };
                for (int i = 0; i < ColorLabels.Length; i++)
                {
                    fields.Add(new XenoFormFieldSpec { Key = (dark ? "d" : "l") + i.ToString(CultureInfo.InvariantCulture), Label = (dark ? "Dark " : "Light ") + ColorLabels[i].ToLowerInvariant(), Value = values[i], Placeholder = "#rrggbb", Required = true });
                }
            }

            Dictionary<string, string>? result = await Context.FormAsync("Custom Colors", "Hex colors such as #4a2054 for light and dark terminals. Secondary text, borders, and status colors are worked out from these. The desktop app uses the same custom colors.", fields, null, "Save", "Saving...").ConfigureAwait(false);
            if (result == null) return;
            List<string> invalid = result.Where(pair => XenoRgb.TryParse(pair.Value) == null).Select(pair => pair.Value).ToList();
            if (invalid.Count > 0)
            {
                Context.Notify("Not a color: " + String.Join(", ", invalid) + ". Use hex such as #4a2054.", NotificationSeverity.Error);
                return;
            }

            foreach (bool dark in new[] { false, true })
            {
                XenoColorSet set = dark ? custom.Dark : custom.Light;
                string Get(int index) => XenoRgb.Parse(result[(dark ? "d" : "l") + index.ToString(CultureInfo.InvariantCulture)]).ToHex();
                set.Background = Get(0);
                set.Panel = Get(1);
                set.Text = Get(2);
                set.Accent = Get(3);
                set.Sidebar = Get(4);
                set.SidebarActive = Get(5);
            }

            Context.Preferences.DesktopCustomColors = custom;
            Context.Preferences.TerminalColorScheme = XenoColorSchemes.CustomId;
            Context.SavePreferences();
            Context.Post(() =>
            {
                _Colors.SelectedIndex = XenoColorSchemes.BuiltIn.Count;
                Context.Restyle();
            });
            IReadOnlyList<string> problems = XenoColorSchemes.Check(custom.Dark, true).Concat(XenoColorSchemes.Check(custom.Light, false)).ToList();
            Context.Notify(problems.Count == 0 ? "Saved your custom colors." : "Saved. " + problems[0], problems.Count == 0 ? NotificationSeverity.Success : NotificationSeverity.Warning);
        }

        #endregion
    }
}

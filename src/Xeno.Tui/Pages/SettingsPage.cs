namespace Xeno.Tui.Pages
{
    using System;
    using System.Collections.Generic;
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

        #endregion
    }
}

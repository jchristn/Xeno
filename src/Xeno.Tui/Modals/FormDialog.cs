namespace Xeno.Tui.Modals
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Modals;
    using TUIKit.Theming;
    using TUIKit.Widgets;
    using Xeno.Client.Presentation;

    /// <summary>
    /// A modal form: labeled text, secret, number, toggle, and choice fields, each with its help text underneath, and a
    /// row of buttons (Save, an optional Save and Add Another, and Cancel) that work with a click or the keyboard. Tab
    /// moves between fields and buttons, Enter moves to the next field and submits from the last one, Ctrl+S submits,
    /// Ctrl+N saves and keeps the form open for another entry (when offered), Ctrl+R shows a secret as typed, and Esc
    /// cancels. A missing required value or a malformed number is shown under its field; an
    /// optional validator can keep the dialog open with an error. Completes with a dictionary of values keyed by field
    /// key, or null when cancelled.
    /// </summary>
    internal sealed class FormDialog : DialogModal, IThemeable
    {
        #region Public-Members

        /// <summary>
        /// Values for the next entry after "save and add another" succeeds, built from the submitted values, or null to
        /// keep every value.
        /// </summary>
        public Func<Dictionary<string, string>, Dictionary<string, string>>? NextValues { get; set; } = null;

        /// <summary>
        /// Confirmation shown after "save and add another" succeeds, built from the submitted values, or null for "Saved.".
        /// </summary>
        public Func<Dictionary<string, string>, string>? AddedMessage { get; set; } = null;

        /// <summary>
        /// Called on the loop thread with the confirmation after each "save and add another" succeeds.
        /// </summary>
        public Action<string>? Added { get; set; } = null;

        /// <summary>
        /// How many entries "save and add another" saved while the dialog was open.
        /// </summary>
        public int AddedCount { get; private set; } = 0;

        /// <summary>
        /// True when the dialog offers "save and add another".
        /// </summary>
        public bool CanAddAnother
        {
            get { return _AddAnotherText != null; }
        }

        #endregion

        #region Protected-Members

        /// <summary>
        /// Pastes go to the focused field.
        /// </summary>
        protected override IPasteTarget? PasteTarget
        {
            get { return _Form; }
        }

        #endregion

        #region Private-Members

        private static readonly KeyChord SubmitChord = KeyChord.Parse("ctrl+s");
        private static readonly KeyChord RevealChord = KeyChord.Parse("ctrl+r");
        private static readonly KeyChord AddAnotherChord = KeyChord.Parse("ctrl+n");
        private readonly Form _Form = new Form();
        private readonly List<XenoFormFieldSpec> _Fields;
        private readonly Dictionary<string, IWidget> _Inputs = new Dictionary<string, IWidget>(StringComparer.Ordinal);
        private readonly Func<Dictionary<string, string>, Task<string?>>? _Validator;
        private readonly Action<Action> _Post;
        private string _Intro;
        private string? _Error = null;
        private string? _Notice = null;
        private readonly string? _AddAnotherText;
        private bool _AnotherPending = false;
        private readonly string _ProgressText;
        private bool _Submitting = false;
        private Theme _Theme = XenoTuiTheme.Dark;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create a form dialog.
        /// </summary>
        /// <param name="title">Title.</param>
        /// <param name="intro">Intro text, or empty.</param>
        /// <param name="fields">Fields.</param>
        /// <param name="validator">Optional async validator; return an error to keep the dialog open.</param>
        /// <param name="post">Marshals a continuation onto the UI loop.</param>
        /// <param name="submitText">Submit action, shown in the footer hint (for example "Save" or "Sign In").</param>
        /// <param name="progressText">Shown while the validator runs (for example "Signing in...").</param>
        /// <param name="addAnotherText">Text of a button that saves and keeps the form open for another entry, or null for none.</param>
        public FormDialog(string title, string intro, IEnumerable<XenoFormFieldSpec> fields, Func<Dictionary<string, string>, Task<string?>>? validator, Action<Action> post, string submitText = "Save", string progressText = "Saving...", string? addAnotherText = null)
        {
            _AddAnotherText = String.IsNullOrWhiteSpace(addAnotherText) ? null : addAnotherText;
            Title = " " + title + " ";
            _ProgressText = String.IsNullOrWhiteSpace(progressText) ? "Saving..." : progressText;
            _Intro = intro ?? String.Empty;
            _Fields = fields.ToList();
            _Validator = validator;
            _Post = post ?? throw new ArgumentNullException(nameof(post));
            FooterHint = " Tab next field  Ctrl+S " + (String.IsNullOrWhiteSpace(submitText) ? "save" : submitText.ToLowerInvariant())
                + (_AddAnotherText != null ? "  Ctrl+N save and add another" : String.Empty)
                + (_Fields.Any(field => field.Secret) ? "  Ctrl+R show" : String.Empty) + "  Esc cancel ";
            MinContentWidth = 56;
            MaxContentWidth = 76;
            _Form.WrapFocus = true;
            _Form.ShowFieldErrors = true;
            _Form.ValidationMode = FormValidationMode.OnSubmit;
            _Form.SubmitKey = SubmitChord;
            _Form.CancelKey = new KeyChord(KeyCode.Escape, 0, KeyModifiers.None);
            _Form.SubmitRequested += (sender, args) => Submit();
            _Form.CancelRequested += (sender, args) => RequestClose(null);
            foreach (XenoFormFieldSpec field in _Fields)
            {
                IWidget input = AddField(field);
                _Inputs[field.Key] = input;
                if (!String.IsNullOrWhiteSpace(field.Help)) _Form.SetFieldHint(_Form.FieldCount - 1, field.Help);
            }

            // Buttons after the last field, so every form can be finished with the mouse as well as the keyboard.
            _Form.ShowButtons = true;
            ButtonRow? buttons = _Form.Buttons;
            if (buttons != null)
            {
                buttons.Clear();
                buttons.Add(String.IsNullOrWhiteSpace(submitText) ? "Save" : submitText, () => _Form.Submit());
                if (_AddAnotherText != null) buttons.Add(_AddAnotherText, SubmitAnother);
                buttons.Add("Cancel", () => RequestClose(null));
            }
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Apply a theme.
        /// </summary>
        /// <param name="theme">Theme.</param>
        public void ApplyTheme(Theme theme)
        {
            _Theme = theme ?? XenoTuiTheme.Dark;
            _Form.ApplyTheme(_Theme);
            ApplyFocusTheme(_Theme);
        }

        /// <summary>
        /// Handle keys: the form takes Tab, Ctrl+S, Esc, and field keys; Enter advances or submits.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <returns>True.</returns>
        public override bool HandleKey(KeyEvent key)
        {
            if (_AddAnotherText != null && AddAnotherChord.Equals(KeyChord.FromKeyEvent(key)))
            {
                SubmitAnother();
                return true;
            }

            if (_Form.HandleKey(key)) return true;
            if (key.Code == KeyCode.Enter)
            {
                if (_Form.FocusedIndex >= _Form.FieldCount - 1) _Form.Submit();
                else _Form.MoveFocus(true);
            }

            return true;
        }

        /// <summary>
        /// Route clicks and the wheel to the form, so a click focuses a field, toggles a checkbox, or opens a choice.
        /// </summary>
        /// <param name="mouse">Mouse event in screen coordinates.</param>
        /// <returns>True.</returns>
        public override bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null || _Submitting) return true;
            Rect content = ContentBounds;
            int top = content.Y + IntroLines(content.Width).Count;
            int x = mouse.X - content.X;
            int y = mouse.Y - top;
            if (x < 0 || x >= content.Width || y < 0) return true;
            _Form.HandleMouse(new MouseEvent(mouse.Kind, mouse.Button, x, y, mouse.Modifiers, mouse.ClickCount));
            return true;
        }

        #endregion

        #region Protected-Methods

        /// <summary>
        /// Content width.
        /// </summary>
        /// <param name="availableWidth">Available width.</param>
        /// <returns>Width.</returns>
        protected override int MeasureContentWidth(int availableWidth)
        {
            return Math.Min(availableWidth, 72);
        }

        /// <summary>
        /// Content height.
        /// </summary>
        /// <param name="contentWidth">Width.</param>
        /// <returns>Height.</returns>
        protected override int MeasureContentHeight(int contentWidth)
        {
            return IntroLines(contentWidth).Count + Math.Max(_Form.ContentHeight, EstimateFormHeight(contentWidth)) + 2;
        }

        /// <summary>
        /// Render intro, form, and error.
        /// </summary>
        /// <param name="content">Surface.</param>
        protected override void RenderContent(ISurface content)
        {
            int width = content.Size.Width;
            List<string> intro = IntroLines(width);
            int row = 0;
            foreach (string line in intro)
            {
                content.DrawText(0, row++, line, _Theme.Muted);
            }

            int formHeight = Math.Max(1, content.Size.Height - row - 2);
            _Form.Render(new SurfaceView(content, new Rect(0, row, width, formHeight)));
            string message = _Submitting ? _ProgressText : _Error ?? _Notice ?? String.Empty;
            if (message.Length > 0)
            {
                CellStyle style = _Submitting ? _Theme.Muted : _Error != null ? _Theme.Error : _Theme.Success;
                content.DrawText(0, content.Size.Height - 1, message.Length > width ? message.Substring(0, width) : message, style);
            }
        }

        #endregion

        #region Private-Methods

        private IWidget AddField(XenoFormFieldSpec field)
        {
            string label = field.Label + (field.Required ? " *" : String.Empty);
            switch (field.Kind)
            {
                case XenoSettingKindEnum.Toggle:
                    return _Form.AddCheckbox(field.Label, String.Equals(field.Value, "true", StringComparison.OrdinalIgnoreCase));
                case XenoSettingKindEnum.Choice:
                    Dropdown<string> dropdown = new Dropdown<string>(field.Choices);
                    int index = field.Choices.FindIndex(item => String.Equals(item, field.Value, StringComparison.OrdinalIgnoreCase));
                    dropdown.SelectedIndex = Math.Max(0, index);
                    return _Form.Add(label, dropdown, () => Check(field, dropdown.SelectedItem ?? String.Empty));
                default:
                    TextField text = new TextField { Value = field.Value, Placeholder = String.IsNullOrEmpty(field.Placeholder) ? null : field.Placeholder, UseLineEditingKeys = true };
                    if (field.Secret)
                    {
                        text.MaskChar = '*';
                        text.RevealKey = RevealChord;
                    }

                    return _Form.Add(label, text, () => Check(field, text.Value));
            }
        }

        private Dictionary<string, string> Collect()
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (XenoFormFieldSpec field in _Fields)
            {
                IWidget input = _Inputs[field.Key];
                if (input is Checkbox check) values[field.Key] = check.Checked ? "true" : "false";
                else if (input is Dropdown<string> dropdown) values[field.Key] = dropdown.SelectedItem ?? String.Empty;
                else if (input is TextField text) values[field.Key] = field.Secret ? text.Value : text.Value.Trim();
                else values[field.Key] = String.Empty;
            }

            return values;
        }

        private void SubmitAnother()
        {
            if (_Submitting || _AddAnotherText == null) return;
            _AnotherPending = true;
            if (!_Form.Submit()) _AnotherPending = false;
        }

        private void Submit()
        {
            bool another = _AnotherPending;
            _AnotherPending = false;
            if (_Submitting) return;
            Dictionary<string, string> values = Collect();
            if (_Validator == null)
            {
                if (another) Saved(values);
                else RequestClose(values);
                return;
            }

            _Submitting = true;
            _Error = null;
            _Notice = null;
            _ = Task.Run(async () =>
            {
                string? error;
                try
                {
                    error = await _Validator(values).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is KeyNotFoundException || ex is System.IO.IOException)
                {
                    error = ex.Message;
                }

                _Post(() =>
                {
                    _Submitting = false;
                    if (!String.IsNullOrWhiteSpace(error)) _Error = error;
                    else if (another) Saved(values);
                    else RequestClose(values);
                });
            });
        }

        private void Saved(Dictionary<string, string> values)
        {
            AddedCount++;
            string message = AddedMessage == null ? "Saved." : AddedMessage(values);
            _Notice = "✓ " + message;
            _Error = null;
            Apply(NextValues == null ? values : NextValues(values));
            Added?.Invoke(message);
        }

        private void Apply(Dictionary<string, string> values)
        {
            int focus = -1;
            for (int i = 0; i < _Fields.Count; i++)
            {
                XenoFormFieldSpec field = _Fields[i];
                if (!values.TryGetValue(field.Key, out string? value)) continue;
                IWidget input = _Inputs[field.Key];
                if (input is TextField text)
                {
                    if (!String.Equals(text.Value, value, StringComparison.Ordinal))
                    {
                        text.Value = value;
                        if (focus < 0 && value.Length == 0) focus = i;
                    }
                }
                else if (input is Checkbox check) check.Checked = String.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                else if (input is Dropdown<string> dropdown)
                {
                    int index = field.Choices.FindIndex(item => String.Equals(item, value, StringComparison.OrdinalIgnoreCase));
                    if (index >= 0) dropdown.SelectedIndex = index;
                }
            }

            _Form.ClearErrors();
            _Form.SetFocusedField(focus >= 0 ? focus : 0);
        }

        private static string? Check(XenoFormFieldSpec field, string value)
        {
            if (field.Required && String.IsNullOrWhiteSpace(value)) return field.Label + " is required.";
            if (field.Kind == XenoSettingKindEnum.Number && !String.IsNullOrWhiteSpace(value) && !Int32.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out _)) return field.Label + " must be a whole number.";
            return null;
        }

        private int EstimateFormHeight(int width)
        {
            // The form's own estimate counts one line per hint; wrapped hints and field errors need more, and a form cut
            // short would hide its buttons.
            int total = 0;
            for (int i = 0; i < _Fields.Count; i++)
            {
                XenoFormFieldSpec field = _Fields[i];
                total += field.Kind == XenoSettingKindEnum.Toggle ? 2 : 3;
                if (!String.IsNullOrWhiteSpace(field.Help)) total += WrapCount(field.Help, width - 2);
                string? error = _Form.GetFieldError(i);
                if (!String.IsNullOrWhiteSpace(error)) total += WrapCount("! " + error, width - 2);
            }

            return total + 1;
        }

        private static int WrapCount(string text, int width)
        {
            if (width < 10) return 1;
            int lines = 1;
            int current = 0;
            foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (current > 0 && current + 1 + word.Length > width)
                {
                    lines++;
                    current = word.Length;
                }
                else
                {
                    current = current == 0 ? word.Length : current + 1 + word.Length;
                }
            }

            return lines;
        }

        private List<string> IntroLines(int width)
        {
            List<string> lines = new List<string>();
            if (String.IsNullOrWhiteSpace(_Intro) || width < 10) return lines;
            string current = String.Empty;
            foreach (string word in _Intro.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (current.Length + word.Length + 1 > width)
                {
                    lines.Add(current);
                    current = word;
                }
                else
                {
                    current = current.Length == 0 ? word : current + " " + word;
                }
            }

            if (current.Length > 0) lines.Add(current);
            lines.Add(String.Empty);
            return lines;
        }

        #endregion
    }
}

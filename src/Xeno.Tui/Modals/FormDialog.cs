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
    /// A modal form: labeled text, secret, number, toggle, and choice fields. Tab moves between fields, Enter moves to
    /// the next field and submits from the last one, Ctrl+S submits, and Esc cancels. An optional validator can keep
    /// the dialog open with an error. Completes with a dictionary of values keyed by field key, or null when cancelled.
    /// </summary>
    internal sealed class FormDialog : DialogModal, IThemeable
    {
        #region Private-Members

        private readonly Form _Form = new Form();
        private readonly List<XenoFormFieldSpec> _Fields;
        private readonly Dictionary<string, IWidget> _Inputs = new Dictionary<string, IWidget>(StringComparer.Ordinal);
        private readonly Func<Dictionary<string, string>, Task<string?>>? _Validator;
        private readonly Action<Action> _Post;
        private string _Intro;
        private string? _Error = null;
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
        public FormDialog(string title, string intro, IEnumerable<XenoFormFieldSpec> fields, Func<Dictionary<string, string>, Task<string?>>? validator, Action<Action> post, string submitText = "Save", string progressText = "Saving...")
        {
            Title = " " + title + " ";
            FooterHint = " Tab next field  Ctrl+S " + (String.IsNullOrWhiteSpace(submitText) ? "save" : submitText.ToLowerInvariant()) + "  Esc cancel ";
            _ProgressText = String.IsNullOrWhiteSpace(progressText) ? "Saving..." : progressText;
            _Intro = intro ?? String.Empty;
            _Fields = fields.ToList();
            _Validator = validator;
            _Post = post ?? throw new ArgumentNullException(nameof(post));
            MinContentWidth = 56;
            MaxContentWidth = 76;
            _Form.WrapFocus = true;
            foreach (XenoFormFieldSpec field in _Fields)
            {
                IWidget input = AddField(field);
                _Inputs[field.Key] = input;
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
        /// Handle keys: Esc cancels, Ctrl+S submits, Enter advances or submits, everything else goes to the form.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <returns>True when handled.</returns>
        public override bool HandleKey(KeyEvent key)
        {
            if (key.Code == KeyCode.Escape) return RequestClose(null);
            if (key.Code == KeyCode.Character && (key.Modifiers & KeyModifiers.Ctrl) != 0 && (key.Rune == 's' || key.Rune == 'S'))
            {
                Submit();
                return true;
            }

            if (_Form.HandleKey(key)) return true;
            if (key.Code == KeyCode.Enter)
            {
                if (_Form.FocusedIndex >= _Form.FieldCount - 1) Submit();
                else _Form.MoveFocus(true);
                return true;
            }

            return true;
        }

        /// <summary>
        /// Insert pasted text into the focused text field.
        /// </summary>
        /// <param name="text">Text.</param>
        /// <returns>True when handled.</returns>
        public override bool HandlePaste(string text)
        {
            if (_Form.FocusedLeaf is TextField field)
            {
                field.Insert(text);
                return true;
            }

            return false;
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
            return IntroLines(contentWidth).Count + _Form.ContentHeight + 2;
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
            string message = _Submitting ? _ProgressText : _Error ?? String.Empty;
            if (message.Length > 0)
            {
                content.DrawText(0, content.Size.Height - 1, message.Length > width ? message.Substring(0, width) : message, _Submitting ? _Theme.Muted : _Theme.Error);
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
                    return _Form.Add(label, dropdown);
                default:
                    TextField text = new TextField { Value = field.Value, Placeholder = String.IsNullOrEmpty(field.Placeholder) ? null : field.Placeholder };
                    if (field.Secret) text.MaskChar = '*';
                    return _Form.Add(label, text);
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

        private void Submit()
        {
            if (_Submitting) return;
            Dictionary<string, string> values = Collect();
            foreach (XenoFormFieldSpec field in _Fields)
            {
                string value = values[field.Key];
                if (field.Required && String.IsNullOrWhiteSpace(value))
                {
                    _Error = field.Label + " is required.";
                    return;
                }

                if (field.Kind == XenoSettingKindEnum.Number && !String.IsNullOrWhiteSpace(value) && !Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                {
                    _Error = field.Label + " must be a whole number.";
                    return;
                }
            }

            if (_Validator == null)
            {
                RequestClose(values);
                return;
            }

            _Submitting = true;
            _Error = null;
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
                    if (String.IsNullOrWhiteSpace(error)) RequestClose(values);
                    else _Error = error;
                });
            });
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

namespace Xeno.Desktop.Common
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Layout;
    using Xeno.Client.Presentation;

    /// <summary>
    /// A modal form built from <see cref="XenoFormField"/> definitions. Returns the field values keyed by
    /// <see cref="XenoFormField.Key"/>, or null when cancelled. An optional validator can reject a submission with a
    /// message; the dialog then stays open. Call from the UI thread.
    /// </summary>
    public sealed class XenoFormDialog : XenoDialogWindow
    {
        #region Public-Members

        /// <summary>
        /// Submit button text while the validator runs (for example "Signing in..."), or null to keep the submit text.
        /// </summary>
        public string? ProgressText { get; set; } = null;

        #endregion

        #region Private-Members

        private readonly List<XenoFormField> _Fields;
        private readonly Dictionary<string, Control> _Inputs = new Dictionary<string, Control>(StringComparer.Ordinal);
        private readonly Func<Dictionary<string, string>, Task<string?>>? _Validator;
        private readonly Border _Error;
        private readonly Button _Submit;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create a form dialog.
        /// </summary>
        /// <param name="title">Window title.</param>
        /// <param name="intro">Text shown above the fields, or null.</param>
        /// <param name="fields">Fields.</param>
        /// <param name="submitText">Submit button text.</param>
        /// <param name="validator">Optional validator; return an error message to keep the dialog open, or null to accept. Runs on the UI thread.</param>
        /// <exception cref="ArgumentNullException">Fields is null.</exception>
        public XenoFormDialog(string title, string? intro, IEnumerable<XenoFormField> fields, string submitText, Func<Dictionary<string, string>, Task<string?>>? validator = null)
        {
            ArgumentNullException.ThrowIfNull(fields);
            _Fields = fields.ToList();
            _Validator = validator;
            Title = title;
            Width = 520;
            SizeToContent = SizeToContent.Height;
            MaxHeight = 760;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Icon = XenoAssets.LoadWindowIcon();
            Background = XenoTheme.Current.Background;

            StackPanel stack = new StackPanel { Spacing = 14, Margin = new Thickness(20) };
            stack.Children.Add(Ui.Title(title));
            if (!String.IsNullOrWhiteSpace(intro)) stack.Children.Add(Ui.Muted(intro));
            foreach (XenoFormField field in _Fields)
            {
                Control input = BuildInput(field);
                _Inputs[field.Key] = input;
                if (field.Kind == XenoFormFieldKindEnum.Check)
                {
                    stack.Children.Add(String.IsNullOrWhiteSpace(field.Help) ? input : Ui.Stack(2, input, Ui.Muted(field.Help)));
                }
                else
                {
                    stack.Children.Add(Ui.Field(field.Label + (field.Required ? " *" : String.Empty), input, field.Help));
                }
            }

            _Error = Ui.Notice(String.Empty, XenoToneEnum.Danger);
            _Error.IsVisible = false;
            stack.Children.Add(_Error);

            Button cancel = Ui.Button("Cancel", () => CloseWith(null));
            _Submit = Ui.Primary(submitText, () => _ = SubmitAsync());
            _Submit.IsDefault = true;
            cancel.IsCancel = true;
            StackPanel buttons = Ui.Row(cancel, _Submit);
            buttons.HorizontalAlignment = HorizontalAlignment.Right;
            stack.Children.Add(buttons);

            Content = new ScrollViewer { Content = stack };
            Opened += (sender, args) =>
            {
                Control? first = _Inputs.Values.FirstOrDefault(item => item is TextBox box && !box.IsReadOnly);
                first?.Focus();
            };
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Show a form and wait for the result.
        /// </summary>
        /// <param name="owner">Owner window, or null to show unowned.</param>
        /// <param name="title">Title.</param>
        /// <param name="intro">Intro text, or null.</param>
        /// <param name="fields">Fields.</param>
        /// <param name="submitText">Submit button text.</param>
        /// <param name="validator">Optional validator.</param>
        /// <param name="progressText">Submit button text while the validator runs, or null to keep the submit text.</param>
        /// <returns>Values, or null when cancelled.</returns>
        public static Task<Dictionary<string, string>?> ShowAsync(Window? owner, string title, string? intro, IEnumerable<XenoFormField> fields, string submitText, Func<Dictionary<string, string>, Task<string?>>? validator = null, string? progressText = null)
        {
            XenoFormDialog dialog = new XenoFormDialog(title, intro, fields, submitText, validator) { ProgressText = progressText };
            return XenoDialogs.ShowModalAsync<Dictionary<string, string>?>(dialog, owner);
        }

        /// <summary>
        /// Show a shared <see cref="XenoFormSpec"/> and run its submit action, keeping the dialog open on errors.
        /// </summary>
        /// <param name="owner">Owner window, or null.</param>
        /// <param name="spec">Form.</param>
        /// <returns>Success message, or null when cancelled.</returns>
        /// <exception cref="ArgumentNullException">Spec is null.</exception>
        public static async Task<string?> ShowSpecAsync(Window? owner, XenoFormSpec spec)
        {
            ArgumentNullException.ThrowIfNull(spec);
            List<XenoFormField> fields = spec.Fields.Select(ToField).ToList();
            Dictionary<string, string>? values = await ShowAsync(owner, spec.Title, String.IsNullOrWhiteSpace(spec.Intro) ? null : spec.Intro, fields, spec.SubmitText,
                form => spec.SubmitAsync(form, System.Threading.CancellationToken.None), spec.ProgressText).ConfigureAwait(true);
            return values == null ? null : spec.SuccessMessage(values);
        }

        /// <summary>
        /// Convert a shared field into a dialog field.
        /// </summary>
        /// <param name="spec">Shared field.</param>
        /// <returns>Dialog field.</returns>
        public static XenoFormField ToField(XenoFormFieldSpec spec)
        {
            ArgumentNullException.ThrowIfNull(spec);
            XenoFormFieldKindEnum kind = XenoFormFieldKindEnum.Text;
            if (spec.Secret) kind = XenoFormFieldKindEnum.Password;
            else if (spec.Kind == XenoSettingKindEnum.Toggle) kind = XenoFormFieldKindEnum.Check;
            else if (spec.Kind == XenoSettingKindEnum.Choice) kind = XenoFormFieldKindEnum.Choice;
            else if (spec.Kind == XenoSettingKindEnum.Number) kind = XenoFormFieldKindEnum.Number;
            return new XenoFormField
            {
                Key = spec.Key,
                Label = spec.Label,
                Kind = kind,
                Value = spec.Value,
                Placeholder = spec.Placeholder,
                Help = spec.Help,
                Choices = spec.Choices,
                Required = spec.Required
            };
        }

        #endregion

        #region Private-Methods

        private static Control BuildInput(XenoFormField field)
        {
            switch (field.Kind)
            {
                case XenoFormFieldKindEnum.Check:
                    return new CheckBox { Content = field.Label, IsChecked = String.Equals(field.Value, "true", StringComparison.OrdinalIgnoreCase), IsEnabled = !field.ReadOnly };
                case XenoFormFieldKindEnum.Choice:
                    ComboBox combo = new ComboBox { ItemsSource = field.Choices, HorizontalAlignment = HorizontalAlignment.Stretch, IsEnabled = !field.ReadOnly };
                    combo.SelectedItem = field.Choices.FirstOrDefault(item => String.Equals(item, field.Value, StringComparison.OrdinalIgnoreCase)) ?? field.Choices.FirstOrDefault();
                    return combo;
                case XenoFormFieldKindEnum.MultiLine:
                    return new TextBox { Text = field.Value, AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap, MinHeight = 90, PlaceholderText = field.Placeholder, IsReadOnly = field.ReadOnly };
                case XenoFormFieldKindEnum.Password:
                    return new TextBox { Text = field.Value, PasswordChar = '•', PlaceholderText = field.Placeholder, IsReadOnly = field.ReadOnly };
                default:
                    return new TextBox { Text = field.Value, PlaceholderText = field.Placeholder, IsReadOnly = field.ReadOnly };
            }
        }

        private Dictionary<string, string> Collect()
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (XenoFormField field in _Fields)
            {
                Control input = _Inputs[field.Key];
                string value;
                if (input is CheckBox check) value = check.IsChecked == true ? "true" : "false";
                else if (input is ComboBox combo) value = combo.SelectedItem as string ?? String.Empty;
                else if (input is TextBox box) value = box.Text ?? String.Empty;
                else value = String.Empty;
                values[field.Key] = field.Kind == XenoFormFieldKindEnum.Password || field.Kind == XenoFormFieldKindEnum.MultiLine ? value : value.Trim();
            }

            return values;
        }

        private async Task SubmitAsync()
        {
            Dictionary<string, string> values = Collect();
            foreach (XenoFormField field in _Fields)
            {
                string value = values[field.Key];
                if (field.Required && String.IsNullOrWhiteSpace(value))
                {
                    ShowError(field.Label + " is required.");
                    return;
                }

                if (field.Kind == XenoFormFieldKindEnum.Number && !String.IsNullOrWhiteSpace(value) && !Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                {
                    ShowError(field.Label + " must be a whole number.");
                    return;
                }
            }

            if (_Validator != null)
            {
                object? submitContent = _Submit.Content;
                _Submit.IsEnabled = false;
                if (!String.IsNullOrWhiteSpace(ProgressText)) _Submit.Content = ProgressText;
                _Error.IsVisible = false;
                try
                {
                    string? error = await _Validator(values);
                    if (!String.IsNullOrWhiteSpace(error))
                    {
                        ShowError(error);
                        return;
                    }
                }
                finally
                {
                    _Submit.Content = submitContent;
                    _Submit.IsEnabled = true;
                }
            }

            CloseWith(values);
        }

        private void ShowError(string message)
        {
            if (_Error.Child is SelectableTextBlock text) text.Text = message;
            _Error.IsVisible = true;
        }

        #endregion
    }
}

namespace Xeno.Client.Presentation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A form shared by the desktop app and the terminal app: its fields, the action that runs on submit, and the
    /// message to report on success. Front ends render the fields, call <see cref="SubmitAsync"/>, keep the form open
    /// when it returns an error, and report <see cref="SuccessMessage"/> otherwise.
    /// </summary>
    public sealed class XenoFormSpec
    {
        /// <summary>
        /// Title.
        /// </summary>
        public string Title { get; set; } = String.Empty;

        /// <summary>
        /// Text shown above the fields.
        /// </summary>
        public string Intro { get; set; } = String.Empty;

        /// <summary>
        /// Submit button text.
        /// </summary>
        public string SubmitText { get; set; } = "Save";

        /// <summary>
        /// Text shown while the form submits.
        /// </summary>
        public string ProgressText { get; set; } = "Saving...";

        /// <summary>
        /// Fields.
        /// </summary>
        public List<XenoFormFieldSpec> Fields { get; set; } = new List<XenoFormFieldSpec>();

        /// <summary>
        /// Runs the change. Returns an error message to keep the form open, or null on success.
        /// </summary>
        public Func<Dictionary<string, string>, CancellationToken, Task<string?>> SubmitAsync { get; set; } = (values, token) => Task.FromResult<string?>(null);

        /// <summary>
        /// Builds the success message from the submitted values.
        /// </summary>
        public Func<Dictionary<string, string>, string> SuccessMessage { get; set; } = values => "Saved.";

        /// <summary>
        /// Text of the second submit button that saves and keeps the form open for another entry (for example
        /// "Save and Add Another"), or null when the form offers only <see cref="SubmitText"/>. Create forms set it;
        /// edit forms leave it null.
        /// </summary>
        public string? AddAnotherText { get; set; } = null;

        /// <summary>
        /// Keys of the fields cleared after "save and add another" succeeds. Every other field keeps its value, so the
        /// next entry starts from the same provider and toggles.
        /// </summary>
        public List<string> ClearOnAddAnother { get; set; } = new List<string>();

        /// <summary>
        /// True when the form offers "save and add another".
        /// </summary>
        public bool CanAddAnother
        {
            get { return !String.IsNullOrWhiteSpace(AddAnotherText); }
        }

        /// <summary>
        /// Values for the next entry after "save and add another" succeeded: the submitted values with the
        /// <see cref="ClearOnAddAnother"/> fields emptied.
        /// </summary>
        /// <param name="submitted">Submitted values.</param>
        /// <returns>Values for the next entry.</returns>
        /// <exception cref="ArgumentNullException">Submitted is null.</exception>
        public Dictionary<string, string> NextValues(IReadOnlyDictionary<string, string> submitted)
        {
            ArgumentNullException.ThrowIfNull(submitted);
            Dictionary<string, string> next = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> pair in submitted)
            {
                next[pair.Key] = ClearOnAddAnother.Contains(pair.Key, StringComparer.Ordinal) ? String.Empty : pair.Value;
            }

            return next;
        }
    }
}

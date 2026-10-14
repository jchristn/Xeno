namespace Xeno.Client.Presentation
{
    using System;
    using System.Collections.Generic;
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
    }
}

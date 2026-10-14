namespace Xeno.Client.Presentation
{
    using System;

    /// <summary>
    /// What a front end shows after an action completes: a title, a message, and an optional next step offered as a
    /// button (for example Start Client after signing in).
    /// </summary>
    public sealed class XenoFollowUp
    {
        /// <summary>
        /// Dialog title.
        /// </summary>
        public string Title { get; set; } = String.Empty;

        /// <summary>
        /// Message. Paragraphs are separated by blank lines.
        /// </summary>
        public string Message { get; set; } = String.Empty;

        /// <summary>
        /// Label of the button that runs <see cref="Command"/>, or empty when there is no next step.
        /// </summary>
        public string ActionText { get; set; } = String.Empty;

        /// <summary>
        /// Command offered as the next step, or null.
        /// </summary>
        public XenoCommandEnum? Command { get; set; } = null;
    }
}

namespace Xeno.Desktop.Common
{
    using System;

    /// <summary>
    /// An action offered for a table row in its context menu.
    /// </summary>
    /// <typeparam name="TRow">Row type.</typeparam>
    public sealed class XenoRowAction<TRow>
    {
        /// <summary>
        /// Display text.
        /// </summary>
        public string Header { get; }

        /// <summary>
        /// Action.
        /// </summary>
        public Action<TRow> Invoke { get; }

        /// <summary>
        /// True for destructive actions.
        /// </summary>
        public bool Destructive { get; set; } = false;

        /// <summary>
        /// Create an action.
        /// </summary>
        /// <param name="header">Display text.</param>
        /// <param name="invoke">Action.</param>
        /// <exception cref="ArgumentNullException">Header or action is null.</exception>
        public XenoRowAction(string header, Action<TRow> invoke)
        {
            Header = header ?? throw new ArgumentNullException(nameof(header));
            Invoke = invoke ?? throw new ArgumentNullException(nameof(invoke));
        }
    }
}

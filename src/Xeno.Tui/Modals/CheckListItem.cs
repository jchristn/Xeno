namespace Xeno.Tui.Modals
{
    using System;

    /// <summary>
    /// One row of a <see cref="CheckListDialog"/>.
    /// </summary>
    internal sealed class CheckListItem
    {
        /// <summary>
        /// Label.
        /// </summary>
        public string Label { get; set; } = String.Empty;

        /// <summary>
        /// True when checked.
        /// </summary>
        public bool Checked { get; set; } = false;

        /// <summary>
        /// False to show the row without letting it change.
        /// </summary>
        public bool Enabled { get; set; } = true;
    }
}

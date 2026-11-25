namespace Xeno.Tui.Pages
{
    using System.Collections.Generic;
    using TUIKit.Widgets;

    /// <summary>
    /// A page table whose columns the user can choose, independent of its row type.
    /// </summary>
    internal interface ITuiTableColumns
    {
        /// <summary>
        /// The table widget.
        /// </summary>
        IWidget Widget { get; }

        /// <summary>
        /// Id under which the choice is stored.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Frame title, or null.
        /// </summary>
        string? Title { get; set; }

        /// <summary>
        /// Every column header, in display order.
        /// </summary>
        IReadOnlyList<string> Columns { get; }

        /// <summary>
        /// Headers shown now, in display order.
        /// </summary>
        IReadOnlyList<string> Visible { get; }

        /// <summary>
        /// Headers shown by default.
        /// </summary>
        IReadOnlyList<string> Defaults { get; }

        /// <summary>
        /// Show exactly these columns.
        /// </summary>
        /// <param name="visible">Headers to show.</param>
        void Apply(IEnumerable<string> visible);
    }
}

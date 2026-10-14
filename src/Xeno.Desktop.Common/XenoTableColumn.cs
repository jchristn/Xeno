namespace Xeno.Desktop.Common
{
    using System;
    using Avalonia.Controls;

    /// <summary>
    /// A column of a <see cref="XenoTable{TRow}"/>.
    /// </summary>
    /// <typeparam name="TRow">Row type.</typeparam>
    public sealed class XenoTableColumn<TRow>
    {
        /// <summary>
        /// Header text.
        /// </summary>
        public string Header { get; }

        /// <summary>
        /// Cell text.
        /// </summary>
        public Func<TRow, string> Text { get; }

        /// <summary>
        /// Column width: a pixel width, or a star weight when <see cref="IsStar"/> is true. Default weight 1.
        /// </summary>
        public double Width { get; set; } = 1;

        /// <summary>
        /// True when <see cref="Width"/> is a star weight.
        /// </summary>
        public bool IsStar { get; set; } = true;

        /// <summary>
        /// Sort key, or null to sort by the cell text.
        /// </summary>
        public Func<TRow, IComparable?>? SortKey { get; set; } = null;

        /// <summary>
        /// Custom cell content, or null to show <see cref="Text"/>.
        /// </summary>
        public Func<TRow, Control>? Cell { get; set; } = null;

        /// <summary>
        /// True to render the cell in the monospace font.
        /// </summary>
        public bool Monospace { get; set; } = false;

        /// <summary>
        /// Create a column.
        /// </summary>
        /// <param name="header">Header text.</param>
        /// <param name="text">Cell text.</param>
        /// <exception cref="ArgumentNullException">Header or text is null.</exception>
        public XenoTableColumn(string header, Func<TRow, string> text)
        {
            Header = header ?? throw new ArgumentNullException(nameof(header));
            Text = text ?? throw new ArgumentNullException(nameof(text));
        }

        /// <summary>
        /// Set a fixed pixel width.
        /// </summary>
        /// <param name="pixels">Width in pixels.</param>
        /// <returns>This column.</returns>
        public XenoTableColumn<TRow> Fixed(double pixels)
        {
            Width = pixels;
            IsStar = false;
            return this;
        }

        /// <summary>
        /// Set a star weight.
        /// </summary>
        /// <param name="weight">Weight.</param>
        /// <returns>This column.</returns>
        public XenoTableColumn<TRow> Star(double weight)
        {
            Width = weight;
            IsStar = true;
            return this;
        }
    }
}

namespace Xeno.Client.Presentation
{
    using System;

    /// <summary>
    /// One column of a <see cref="CliTable"/>.
    /// </summary>
    public sealed class CliTableColumn
    {
        /// <summary>
        /// Name used to pick the column with --columns, compared case-insensitively.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Header text.
        /// </summary>
        public string Header { get; set; } = String.Empty;

        /// <summary>
        /// Longest value shown before it is clipped with an ellipsis; 0 for no limit.
        /// </summary>
        public int MaxWidth { get; set; } = 0;

        /// <summary>
        /// Right-align values, for numbers and sizes.
        /// </summary>
        public bool RightAligned { get; set; } = false;

        /// <summary>
        /// Shown when --columns is not given. Columns that are not shown by default keep tables narrow and can still be picked.
        /// </summary>
        public bool DefaultVisible { get; set; } = true;
    }
}

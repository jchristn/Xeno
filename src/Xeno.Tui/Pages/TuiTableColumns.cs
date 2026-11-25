namespace Xeno.Tui.Pages
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using TUIKit.Widgets;

    /// <summary>
    /// Column choice for one <see cref="DataTable{T}"/>. The defaults are the columns visible when the page finished
    /// building the table.
    /// </summary>
    /// <typeparam name="T">Row type.</typeparam>
    internal sealed class TuiTableColumns<T> : ITuiTableColumns
    {
        #region Public-Members

        /// <inheritdoc />
        public IWidget Widget
        {
            get { return _Table; }
        }

        /// <inheritdoc />
        public string Id { get; }

        /// <inheritdoc />
        public string? Title { get; set; } = null;

        /// <inheritdoc />
        public IReadOnlyList<string> Columns
        {
            get { return _Table.Columns.Select(column => column.Name).ToList(); }
        }

        /// <inheritdoc />
        public IReadOnlyList<string> Visible
        {
            get { return _Table.VisibleColumns.Select(column => column.Name).ToList(); }
        }

        /// <inheritdoc />
        public IReadOnlyList<string> Defaults
        {
            get
            {
                if (_Defaults == null) _Defaults = Visible.ToList();
                return _Defaults;
            }
        }

        #endregion

        #region Private-Members

        private readonly DataTable<T> _Table;
        private List<string>? _Defaults = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Track a table.
        /// </summary>
        /// <param name="table">Table.</param>
        /// <param name="id">Id under which the choice is stored.</param>
        public TuiTableColumns(DataTable<T> table, string id)
        {
            _Table = table ?? throw new ArgumentNullException(nameof(table));
            Id = id ?? throw new ArgumentNullException(nameof(id));
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public void Apply(IEnumerable<string> visible)
        {
            ArgumentNullException.ThrowIfNull(visible);
            List<string> names = visible.ToList();

            // DataTable matches by column key, which is the header unless a page set one.
            List<string> keys = _Table.Columns.Where(column => names.Contains(column.Name, StringComparer.Ordinal)).Select(column => column.Key).ToList();
            if (keys.Count > 0) _Table.SetVisibleColumns(keys);
        }

        #endregion
    }
}

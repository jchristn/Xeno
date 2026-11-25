namespace Xeno.Tui.Pages
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using TUIKit.Input;
    using TUIKit.Layout;
    using TUIKit.Widgets;
    using Xeno.Client.Presentation;
    using Xeno.Client.Services;
    using Xeno.Tui.Modals;
    using Xeno.Tui.Shell;

    /// <summary>
    /// Base class for terminal app pages. A page builds its widget tree once, refreshes data in the background, and
    /// applies results on the loop thread.
    /// </summary>
    internal abstract class TuiPage
    {
        #region Public-Members

        /// <summary>
        /// Page.
        /// </summary>
        public abstract XenoPageEnum Page { get; }

        /// <summary>
        /// Root widget bound into the main region.
        /// </summary>
        public abstract IWidget Root { get; }

        /// <summary>
        /// Page key hints for the status bar.
        /// </summary>
        public virtual IReadOnlyList<KeyHint> Hints
        {
            get { return new List<KeyHint>(); }
        }

        /// <summary>
        /// Page key hints plus the Columns hint when the page has tables.
        /// </summary>
        public IReadOnlyList<KeyHint> AllHints
        {
            get
            {
                List<KeyHint> hints = new List<KeyHint>(Hints);
                if (_Tables.Count > 0) hints.Add(new KeyHint(ColumnsKey.ToString(), XenoTableColumns.ControlText));
                return hints;
            }
        }

        /// <summary>
        /// Tables on the page whose columns the user can choose.
        /// </summary>
        public IReadOnlyList<ITuiTableColumns> ColumnTables
        {
            get { return _Tables; }
        }

        /// <summary>
        /// Key that opens the column chooser for the focused table.
        /// </summary>
        public const char ColumnsKey = 'v';

        /// <summary>
        /// True when the page's charts follow the auto-refresh choice right now.
        /// </summary>
        public virtual bool AutoRefreshes
        {
            get { return false; }
        }

        /// <summary>
        /// True when the page should refresh on the status timer.
        /// </summary>
        public virtual bool RefreshWithStatus
        {
            get { return false; }
        }

        #endregion

        #region Protected-Members

        /// <summary>
        /// Context.
        /// </summary>
        protected TuiContext Context { get; }

        /// <summary>
        /// Workspace.
        /// </summary>
        protected XenoWorkspace Workspace
        {
            get { return Context.Workspace; }
        }

        #endregion

        #region Private-Members

        private readonly List<Action<string>> _LoadFailed = new List<Action<string>>();
        private readonly List<ITuiTableColumns> _Tables = new List<ITuiTableColumns>();
        private bool _ColumnsApplied = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create a page.
        /// </summary>
        /// <param name="context">Context.</param>
        protected TuiPage(TuiContext context)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Reload data.
        /// </summary>
        /// <returns>Task.</returns>
        public abstract Task RefreshAsync();

        /// <summary>
        /// Handle a page-level key that the focused widget did not use.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <returns>True when handled.</returns>
        public virtual bool HandleKey(KeyEvent key)
        {
            return false;
        }

        /// <summary>
        /// Show each table's stored column choice, or its defaults. Called once after the page is built.
        /// </summary>
        public void ApplyColumnPreferences()
        {
            if (_ColumnsApplied) return;
            _ColumnsApplied = true;
            foreach (ITuiTableColumns table in _Tables)
            {
                IReadOnlyList<string> defaults = table.Defaults;
                table.Apply(XenoTableColumns.Visible(Context.Preferences, table.Id, table.Columns, defaults.ToList()));
            }
        }

        /// <summary>
        /// Handle the page keys every page shares: the Columns key opens the chooser for the focused table.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <returns>True when handled.</returns>
        public bool HandleSharedKey(KeyEvent key)
        {
            if (_Tables.Count == 0 || !PageKeys.Is(key, ColumnsKey)) return false;
            _ = ChooseColumnsAsync(FocusedTable() ?? _Tables[0]);
            return true;
        }

        /// <summary>
        /// Let the user choose a table's columns in a checklist, then show and remember the choice.
        /// </summary>
        /// <param name="table">Table.</param>
        /// <returns>Task that completes when the chooser closes.</returns>
        public Task ChooseColumnsAsync(ITuiTableColumns table)
        {
            return Context.ChooseColumnsAsync(table);
        }

        /// <summary>
        /// Called on the loop thread when the client status changes.
        /// </summary>
        /// <param name="status">Status.</param>
        public virtual void OnStatusChanged(ClientStatusSnapshot status)
        {
        }

        #endregion

        #region Protected-Methods

        /// <summary>
        /// Add a pane to a framed stack with one empty column between its frame and its content on each side (see
        /// <see cref="Inset"/>). Nested framed stacks are added as they are. A dropdown is wrapped in a
        /// <see cref="DropdownPicker"/>, which shows its choices as a popup, since a one-row pane has no room for the list.
        /// </summary>
        /// <param name="stack">Stack.</param>
        /// <param name="child">Pane content.</param>
        /// <param name="size">Size along the stack.</param>
        /// <param name="title">Frame title, or null.</param>
        protected void Frame(FramedStack stack, IWidget child, StackSize size, string? title = null)
        {
            ArgumentNullException.ThrowIfNull(stack);
            ArgumentNullException.ThrowIfNull(child);
            ITuiTableColumns? columns = _Tables.FirstOrDefault(item => ReferenceEquals(item.Widget, child));
            if (columns != null && title != null) columns.Title = title;
            if (child is Dropdown<string> dropdown)
            {
                DropdownPicker picker = new DropdownPicker(dropdown);
                picker.Open = item => _ = Context.ChooseAsync(item);
                child = picker;
            }

            stack.Add(Inset.Around(child), size, title);
        }

        /// <summary>
        /// Fetch the page's data in the background, then apply it on the loop thread. A failure is reported on the
        /// message line, and a table still waiting for its first rows shows the error instead of "Loading...".
        /// </summary>
        /// <typeparam name="T">Data type.</typeparam>
        /// <param name="fetch">Background fetch.</param>
        /// <param name="apply">Applies the result on the loop thread.</param>
        /// <returns>Task that completes after the result is posted.</returns>
        protected Task LoadAsync<T>(Func<CancellationToken, Task<T>> fetch, Action<T> apply)
        {
            return Context.LoadAsync(fetch, apply, message =>
            {
                foreach (Action<string> failed in _LoadFailed) failed(message);
            });
        }

        /// <summary>
        /// Create a data table with standard styling and an empty-state message. The table shows "Loading..." until
        /// the page binds its first rows (see <see cref="LoadAsync"/>), so the empty-state message never appears
        /// before the data has been read.
        /// </summary>
        /// <typeparam name="T">Row type.</typeparam>
        /// <param name="emptyText">Empty-state text.</param>
        /// <param name="key">Row identity for keeping the selection across refreshes.</param>
        /// <returns>Table.</returns>
        protected DataTable<T> Table<T>(string emptyText, Func<T, string> key)
        {
            DataTable<T> table = new DataTable<T> { EmptyText = emptyText, KeySelector = key, StretchLastColumn = true };
            table.SetLoading();
            _Tables.Add(new TuiTableColumns<T>(table, "terminal:" + GetType().Name + ":" + _Tables.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            _LoadFailed.Add(message =>
            {
                if (table.State == DataTableState.Loading) table.SetError(message);
            });
            return table;
        }

        /// <summary>
        /// Add a sortable column with a fixed width.
        /// </summary>
        /// <typeparam name="T">Row type.</typeparam>
        /// <param name="table">Table.</param>
        /// <param name="name">Header.</param>
        /// <param name="value">Cell text.</param>
        /// <param name="width">Width in columns, or 0 to share the remaining width.</param>
        /// <param name="visible">False to hide the column by default; users can show it with the Columns control.</param>
        /// <returns>The table.</returns>
        protected static DataTable<T> Column<T>(DataTable<T> table, string name, Func<T, string> value, int width, bool visible = true)
        {
            DataColumn<T> column = new DataColumn<T>(name, value, true);
            if (width > 0) column.Width = width;
            column.Visible = visible;
            return table.Column(column);
        }

        #endregion

        #region Private-Methods

        private ITuiTableColumns? FocusedTable()
        {
            IFocusable? leaf = Root is IFocusContainer container ? container.FocusedLeaf : Root as IFocusable;
            return _Tables.FirstOrDefault(item => ReferenceEquals(item.Widget, leaf));
        }

        #endregion
    }
}

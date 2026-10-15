namespace Xeno.Tui.Pages
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using TUIKit.Input;
    using TUIKit.Layout;
    using TUIKit.Widgets;
    using Xeno.Client.Presentation;
    using Xeno.Client.Services;
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
        /// <returns>The table.</returns>
        protected static DataTable<T> Column<T>(DataTable<T> table, string name, Func<T, string> value, int width)
        {
            DataColumn<T> column = new DataColumn<T>(name, value, true);
            if (width > 0) column.Width = width;
            return table.Column(column);
        }

        #endregion
    }
}

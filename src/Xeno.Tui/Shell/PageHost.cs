namespace Xeno.Tui.Shell
{
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Theming;
    using TUIKit.Widgets;

    /// <summary>
    /// The main region's widget: forwards rendering, keys, focus, mouse, hints, and theme to the current page's root
    /// widget, so navigation swaps pages without rebinding regions.
    /// </summary>
    internal sealed class PageHost : IWidget, IFocusContainer, IFocusAware, IFocusPathNode, IMouseAware, IKeyHintSource, IThemeable
    {
        #region Public-Members

        /// <summary>
        /// Current page root, or null.
        /// </summary>
        public IWidget? Content
        {
            get { return _Content; }
            set
            {
                if (_Focused && _Content is IFocusAware previous) previous.OnFocusChanged(false);
                _Content = value;
                if (_Theme != null && value is IThemeable themeable) themeable.ApplyTheme(_Theme);
                if (value is IFocusContainer container) container.FocusEdge(true);
                if (_Focused && value is IFocusAware next) next.OnFocusChanged(true);
            }
        }

        /// <summary>
        /// Page-level key handler, tried when the focused widget does not use a key (single-letter actions).
        /// </summary>
        public System.Func<KeyEvent, bool>? Fallback { get; set; } = null;

        /// <summary>
        /// Tables on the page that get a Columns button on their frame, or null.
        /// </summary>
        public IReadOnlyList<Pages.ITuiTableColumns>? ColumnTables { get; set; } = null;

        /// <summary>
        /// Opens the column chooser for a table, or null.
        /// </summary>
        public System.Action<Pages.ITuiTableColumns>? OpenColumns { get; set; } = null;

        /// <summary>
        /// Where each table's Columns button was drawn in the last render, in this widget's coordinates.
        /// </summary>
        public IReadOnlyList<KeyValuePair<Pages.ITuiTableColumns, Rect>> ColumnButtons
        {
            get { return _Buttons; }
        }

        /// <summary>
        /// Label of the Columns button drawn on a table's frame.
        /// </summary>
        public const string ColumnsButtonText = "[" + Xeno.Client.Presentation.XenoTableColumns.ControlText + "]";

        /// <summary>
        /// Extra key hints contributed by the current page.
        /// </summary>
        public IReadOnlyList<KeyHint>? Hints { get; set; } = null;

        /// <summary>
        /// Focused leaf of the current page.
        /// </summary>
        public IFocusable? FocusedLeaf
        {
            get
            {
                if (_Content is IFocusContainer container) return container.FocusedLeaf;
                return _Content as IFocusable;
            }
        }

        /// <summary>
        /// Focused child of the current page.
        /// </summary>
        public IFocusable? FocusedChild
        {
            get
            {
                if (_Content is IFocusPathNode node) return node.FocusedChild;
                return _Content as IFocusable;
            }
        }

        #endregion

        #region Private-Members

        private IWidget? _Content = null;
        private bool _Focused = false;
        private Theme? _Theme = null;
        private readonly List<KeyValuePair<Pages.ITuiTableColumns, Rect>> _Buttons = new List<KeyValuePair<Pages.ITuiTableColumns, Rect>>();

        #endregion

        #region Public-Methods

        /// <summary>
        /// Measure.
        /// </summary>
        /// <param name="available">Available size.</param>
        /// <returns>Desired size.</returns>
        public Size Measure(Size available)
        {
            return available;
        }

        /// <summary>
        /// Render the current page.
        /// </summary>
        /// <param name="surface">Surface.</param>
        public void Render(ISurface surface)
        {
            _Content?.Render(surface);
            _Buttons.Clear();
            if (_Content == null || ColumnTables == null) return;

            // Each table's Columns button sits on the right of the top border of its pane.
            Rect area = new Rect(0, 0, surface.Size.Width, surface.Size.Height);
            CellStyle style = _Theme == null ? CellStyle.Default : _Theme.Accent;
            foreach (Pages.ITuiTableColumns table in ColumnTables)
            {
                Rect? found = WidgetLocator.Locate(_Content, area, table.Widget);
                if (found == null) continue;
                Rect rect = found.Value;
                int y = rect.Y - 1;
                int x = rect.X + rect.Width - ColumnsButtonText.Length;
                int title = table.Title == null ? 0 : table.Title.Length + 4;
                if (y < 0 || x < rect.X + title) continue;
                surface.DrawText(x, y, ColumnsButtonText, style);
                _Buttons.Add(new KeyValuePair<Pages.ITuiTableColumns, Rect>(table, new Rect(x, y, ColumnsButtonText.Length, 1)));
            }
        }

        /// <summary>
        /// Forward a key to the current page.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <returns>True when handled.</returns>
        public bool HandleKey(KeyEvent key)
        {
            if (_Content is IFocusable focusable && focusable.HandleKey(key)) return true;
            return Fallback != null && Fallback(key);
        }

        /// <summary>
        /// Move focus within the current page.
        /// </summary>
        /// <param name="forward">Direction.</param>
        /// <returns>True when focus moved within the page.</returns>
        public bool MoveFocus(bool forward)
        {
            return _Content is IFocusContainer container && container.MoveFocus(forward);
        }

        /// <summary>
        /// Focus the first or last stop of the current page.
        /// </summary>
        /// <param name="first">True for the first stop.</param>
        public void FocusEdge(bool first)
        {
            if (_Content is IFocusContainer container) container.FocusEdge(first);
        }

        /// <summary>
        /// Track focus and forward it to the current page.
        /// </summary>
        /// <param name="focused">True when focused.</param>
        public void OnFocusChanged(bool focused)
        {
            _Focused = focused;
            if (_Content is IFocusAware aware) aware.OnFocusChanged(focused);
        }

        /// <summary>
        /// Forward a mouse event to the current page.
        /// </summary>
        /// <param name="mouse">Mouse event.</param>
        /// <returns>True when handled.</returns>
        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse != null && mouse.Kind == MouseEventKind.Press && mouse.Button == MouseButton.Left && OpenColumns != null)
            {
                foreach (KeyValuePair<Pages.ITuiTableColumns, Rect> button in _Buttons)
                {
                    if (!button.Value.Contains(new Point(mouse.X, mouse.Y))) continue;
                    OpenColumns(button.Key);
                    return true;
                }
            }

            return _Content is IMouseAware aware && aware.HandleMouse(mouse!);
        }

        /// <summary>
        /// Key hints of the current page.
        /// </summary>
        /// <returns>Hints, or null.</returns>
        public IReadOnlyList<KeyHint>? GetKeyHints()
        {
            return Hints;
        }

        /// <summary>
        /// Apply a theme to the current page and remember it for later pages.
        /// </summary>
        /// <param name="theme">Theme.</param>
        public void ApplyTheme(Theme theme)
        {
            _Theme = theme;
            if (_Content is IThemeable themeable) themeable.ApplyTheme(theme);
        }

        #endregion
    }
}

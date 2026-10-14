namespace Xeno.Tui.Modals
{
    using System;
    using System.Linq;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Input;
    using TUIKit.Modals;
    using TUIKit.Theming;
    using TUIKit.Widgets;

    /// <summary>
    /// A scrollable, read-only Markdown viewer for request details, About, and other long text. The Markdown is laid
    /// out at the dialog's width by TUIKit (tables sized from content, wrapped lists, highlighted code). Arrow keys,
    /// Page Up, Page Down, Home, End, and the mouse wheel scroll; Esc, Enter, or q closes.
    /// </summary>
    internal sealed class MarkdownViewerModal : DialogModal, IThemeable
    {
        #region Public-Members

        /// <summary>
        /// Markdown shown.
        /// </summary>
        public string Markdown
        {
            get { return _View.Markdown; }
        }

        #endregion

        #region Private-Members

        private const int MinimumWidth = 60;
        private const int MaximumWidth = 120;
        private const int WheelRows = 3;
        private readonly MarkdownView _View;
        private int _Visible = 10;
        private int _Width = MinimumWidth;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create a viewer.
        /// </summary>
        /// <param name="title">Title.</param>
        /// <param name="markdown">Markdown.</param>
        public MarkdownViewerModal(string title, string markdown)
        {
            Title = " " + title + " ";
            FooterHint = " ↑↓ PgUp PgDn scroll  Esc close ";
            _View = new MarkdownView(markdown ?? String.Empty, Options(XenoTuiTheme.Dark));
            MinContentWidth = MinimumWidth;
            MaxContentWidth = MaximumWidth;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Render options for Markdown in the terminal app: theme colors, rounded table borders, and tables that stay
        /// grids (a long value such as a file path wraps inside a column of at most 64 cells).
        /// </summary>
        /// <param name="theme">Theme.</param>
        /// <returns>Options.</returns>
        public static MarkdownRenderOptions Options(Theme theme)
        {
            MarkdownRenderOptions options = new MarkdownRenderOptions();
            options.Styles = MarkdownStyles.FromTheme(theme ?? XenoTuiTheme.Dark);
            options.Table.Glyphs = MarkdownTableGlyphs.Rounded;
            options.Table.MaxColumnWidth = 64;
            options.Table.MaxRowLines = 6;
            return options;
        }

        /// <summary>
        /// Plain text of the rendered rows at a width, for tests.
        /// </summary>
        /// <param name="width">Width in columns.</param>
        /// <returns>Rows.</returns>
        public string[] RenderedRows(int width)
        {
            return _View.RowsAt(Math.Max(1, width)).Select(row => row.ToPlainString()).ToArray();
        }

        /// <summary>
        /// Apply a theme.
        /// </summary>
        /// <param name="theme">Theme.</param>
        public void ApplyTheme(Theme theme)
        {
            Theme applied = theme ?? XenoTuiTheme.Dark;
            _View.Options = Options(applied);
            ApplyFocusTheme(applied);
        }

        /// <summary>
        /// Scroll or close.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <returns>True.</returns>
        public override bool HandleKey(KeyEvent key)
        {
            switch (key.Code)
            {
                case KeyCode.Escape:
                case KeyCode.Enter:
                    return RequestClose(null);
                case KeyCode.Up: return Scroll(-1);
                case KeyCode.Down: return Scroll(1);
                case KeyCode.PageUp: return Scroll(-_Visible);
                case KeyCode.PageDown: return Scroll(_Visible);
                case KeyCode.Home: return Scroll(Int32.MinValue / 2);
                case KeyCode.End: return Scroll(Int32.MaxValue / 2);
                case KeyCode.Character:
                    if (key.Rune == 'q') return RequestClose(null);
                    return true;
                default:
                    return true;
            }
        }

        /// <summary>
        /// Scroll on the mouse wheel.
        /// </summary>
        /// <param name="mouse">Mouse event.</param>
        /// <returns>True when the wheel scrolled.</returns>
        public override bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null) return false;
            if (mouse.Button == MouseButton.WheelUp) return Scroll(-WheelRows);
            if (mouse.Button == MouseButton.WheelDown) return Scroll(WheelRows);
            return false;
        }

        #endregion

        #region Protected-Methods

        /// <summary>
        /// Content width: the Markdown's natural width, within the viewer's limits.
        /// </summary>
        /// <param name="availableWidth">Available width.</param>
        /// <returns>Width.</returns>
        protected override int MeasureContentWidth(int availableWidth)
        {
            int natural = MarkdownRenderer.MeasureWidth(_View.Markdown, _View.Options);
            return Math.Max(1, Math.Min(availableWidth, Math.Clamp(natural, MinimumWidth, MaximumWidth)));
        }

        /// <summary>
        /// Content height: the rendered rows at the content width.
        /// </summary>
        /// <param name="contentWidth">Width.</param>
        /// <returns>Height.</returns>
        protected override int MeasureContentHeight(int contentWidth)
        {
            return _View.RowsAt(Math.Max(1, contentWidth)).Count;
        }

        /// <summary>
        /// Render the visible rows.
        /// </summary>
        /// <param name="content">Surface.</param>
        protected override void RenderContent(ISurface content)
        {
            _Visible = Math.Max(1, content.Size.Height);
            _Width = Math.Max(1, content.Size.Width);
            _View.Render(content);
        }

        #endregion

        #region Private-Methods

        private bool Scroll(int rows)
        {
            int max = Math.Max(0, _View.RowsAt(_Width).Count - _Visible);
            long top = (long)Math.Min(_View.ScrollOffset, max) + rows;
            _View.ScrollOffset = (int)Math.Clamp(top, 0, max);
            return true;
        }

        #endregion
    }
}

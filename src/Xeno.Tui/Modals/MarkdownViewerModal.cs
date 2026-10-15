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
    /// out at the dialog's width by TUIKit (tables sized from content, wrapped lists, highlighted code) and follows the
    /// app theme. TUIKit's <see cref="MarkdownView"/> scrolls on the arrow keys, Page Up, Page Down, Home, End, and the
    /// mouse wheel; Esc, Enter, or q closes.
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
        private readonly MarkdownView _View;

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
            FooterHint = " ↑↓ PgUp PgDn Home End scroll  Esc close ";
            _View = new MarkdownView(markdown ?? String.Empty, Options()) { UseThemeStyles = true, ShowKeyHints = false };
            _View.ApplyTheme(XenoTuiTheme.Dark);
            MinContentWidth = MinimumWidth;
            MaxContentWidth = MaximumWidth;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Render options for Markdown in the terminal app: rounded table borders, and tables that stay grids (a long
        /// value such as a file path wraps inside a column of at most 64 cells). The viewer adds the theme's colors.
        /// </summary>
        /// <returns>Options.</returns>
        public static MarkdownRenderOptions Options()
        {
            MarkdownRenderOptions options = new MarkdownRenderOptions();
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
            _View.ApplyTheme(applied);
            ApplyFocusTheme(applied);
        }

        /// <summary>
        /// Close on Esc, Enter, or q; the view scrolls on the other keys.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <returns>True.</returns>
        public override bool HandleKey(KeyEvent key)
        {
            if (key.Code == KeyCode.Escape || key.Code == KeyCode.Enter || (key.Code == KeyCode.Character && key.Rune == 'q')) return RequestClose(null);
            _View.HandleKey(key);
            return true;
        }

        /// <summary>
        /// Scroll on the mouse wheel.
        /// </summary>
        /// <param name="mouse">Mouse event in screen coordinates.</param>
        /// <returns>True when the view used the event.</returns>
        public override bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null) return false;
            Rect content = ContentBounds;
            return _View.HandleMouse(new MouseEvent(mouse.Kind, mouse.Button, mouse.X - content.X, mouse.Y - content.Y, mouse.Modifiers, mouse.ClickCount));
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
            _View.Render(content);
        }

        #endregion
    }
}

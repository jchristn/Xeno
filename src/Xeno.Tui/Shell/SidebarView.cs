namespace Xeno.Tui.Shell
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Theming;
    using TUIKit.Widgets;
    using Xeno.Client.Presentation;

    /// <summary>
    /// Navigation sidebar built from the shared <see cref="XenoNavigationCatalog"/>: section headings and pages.
    /// Up and Down move the highlight, Enter or a click opens the page.
    /// </summary>
    internal sealed class SidebarView : IWidget, IFocusable, IFocusAware, IMouseAware, IKeyHintSource, IThemeable
    {
        #region Public-Members

        /// <summary>
        /// Raised when a page is chosen.
        /// </summary>
        public event Action<XenoPageEnum>? PageChosen;

        /// <summary>
        /// Page currently shown in the main region.
        /// </summary>
        public XenoPageEnum Current { get; set; } = XenoPageEnum.Overview;

        /// <summary>
        /// Highlighted page.
        /// </summary>
        public XenoPageEnum Highlighted
        {
            get { return XenoNavigationCatalog.Pages[_Highlight].Page; }
        }

        #endregion

        #region Private-Members

        private int _Highlight = 0;
        private bool _Focused = false;
        private Theme _Theme = XenoTuiTheme.Dark;
        private readonly List<int> _RowToPage = new List<int>();

        #endregion

        #region Public-Methods

        /// <summary>
        /// Move the highlight to a page.
        /// </summary>
        /// <param name="page">Page.</param>
        public void Highlight(XenoPageEnum page)
        {
            int index = XenoNavigationCatalog.Pages.ToList().FindIndex(item => item.Page == page);
            if (index >= 0) _Highlight = index;
        }

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
        /// Render sections and pages.
        /// </summary>
        /// <param name="surface">Surface.</param>
        public void Render(ISurface surface)
        {
            _RowToPage.Clear();
            int width = surface.Size.Width;
            int row = 0;
            string? section = null;
            for (int i = 0; i < XenoNavigationCatalog.Pages.Count && row < surface.Size.Height; i++)
            {
                XenoPageDefinition page = XenoNavigationCatalog.Pages[i];
                if (page.Section != section)
                {
                    if (section != null)
                    {
                        _RowToPage.Add(-1);
                        row++;
                    }

                    section = page.Section;
                    if (row >= surface.Size.Height) break;
                    surface.DrawText(1, row, Fit(section.ToUpperInvariant(), width - 1), _Theme.Muted);
                    _RowToPage.Add(-1);
                    row++;
                    if (row >= surface.Size.Height) break;
                }

                bool current = page.Page == Current;
                bool highlighted = i == _Highlight;
                CellStyle style = highlighted && _Focused ? _Theme.Selection : current ? _Theme.Accent.WithAttribute(CellAttributes.Bold, true) : _Theme.Text;
                string marker = current ? "▸ " : "  ";
                string text = marker + page.Title;
                surface.DrawText(1, row, Fit(text.PadRight(Math.Max(0, width - 2)), width - 1), style);
                _RowToPage.Add(i);
                row++;
            }
        }

        /// <summary>
        /// Handle navigation keys.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <returns>True when handled.</returns>
        public bool HandleKey(KeyEvent key)
        {
            switch (key.Code)
            {
                case KeyCode.Up:
                    _Highlight = (_Highlight - 1 + XenoNavigationCatalog.Pages.Count) % XenoNavigationCatalog.Pages.Count;
                    return true;
                case KeyCode.Down:
                    _Highlight = (_Highlight + 1) % XenoNavigationCatalog.Pages.Count;
                    return true;
                case KeyCode.Home:
                    _Highlight = 0;
                    return true;
                case KeyCode.End:
                    _Highlight = XenoNavigationCatalog.Pages.Count - 1;
                    return true;
                case KeyCode.Enter:
                    PageChosen?.Invoke(Highlighted);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Open the page under a click.
        /// </summary>
        /// <param name="mouse">Mouse event.</param>
        /// <returns>True when handled.</returns>
        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse.Kind != MouseEventKind.Press || mouse.Button != MouseButton.Left) return false;
            if (mouse.Y < 0 || mouse.Y >= _RowToPage.Count) return false;
            int index = _RowToPage[mouse.Y];
            if (index < 0) return false;
            _Highlight = index;
            PageChosen?.Invoke(Highlighted);
            return true;
        }

        /// <summary>
        /// Track focus.
        /// </summary>
        /// <param name="focused">True when focused.</param>
        public void OnFocusChanged(bool focused)
        {
            _Focused = focused;
            if (focused) Highlight(Current);
        }

        /// <summary>
        /// Key hints.
        /// </summary>
        /// <returns>Hints.</returns>
        public IReadOnlyList<KeyHint>? GetKeyHints()
        {
            return new List<KeyHint> { new KeyHint("↑↓", "Move"), new KeyHint("Enter", "Open") };
        }

        /// <summary>
        /// Apply a theme.
        /// </summary>
        /// <param name="theme">Theme.</param>
        public void ApplyTheme(Theme theme)
        {
            _Theme = theme ?? XenoTuiTheme.Dark;
        }

        #endregion

        #region Private-Methods

        private static string Fit(string text, int width)
        {
            if (width <= 0) return String.Empty;
            return text.Length <= width ? text : text.Substring(0, width);
        }

        #endregion
    }
}

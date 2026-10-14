namespace Xeno.Tui.Pages
{
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Input;
    using TUIKit.Widgets;

    /// <summary>
    /// A focusable, scrollable log viewer over a TUIKit pane. Arrow keys and Page Up and Page Down scroll; End returns
    /// to the live tail.
    /// </summary>
    internal sealed class LogView : IWidget, IFocusable, IMouseAware
    {
        #region Private-Members

        private readonly Pane _Pane = new Pane("log") { ScrollbackCapacityLines = 10000 };

        #endregion

        #region Public-Methods

        /// <summary>
        /// Replace the shown lines, keeping the scroll position at the tail when it was there.
        /// </summary>
        /// <param name="lines">Lines.</param>
        public void SetLines(IReadOnlyList<string> lines)
        {
            bool atBottom = _Pane.IsAtBottom;
            using (_Pane.BeginBatch())
            {
                _Pane.Clear();
                foreach (string line in lines) _Pane.WriteLine(line);
            }

            if (atBottom) _Pane.ScrollToBottom();
        }

        /// <summary>
        /// Plain text of the lines, for tests.
        /// </summary>
        /// <returns>Lines.</returns>
        public IReadOnlyList<string> Snapshot()
        {
            return _Pane.SnapshotPlainLines();
        }

        /// <summary>
        /// Scroll keys.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <returns>True when handled.</returns>
        public bool HandleKey(KeyEvent key)
        {
            switch (key.Code)
            {
                case KeyCode.Up: _Pane.ScrollUp(1); return true;
                case KeyCode.Down: _Pane.ScrollDown(1); return true;
                case KeyCode.PageUp: _Pane.ScrollUp(10); return true;
                case KeyCode.PageDown: _Pane.ScrollDown(10); return true;
                case KeyCode.End: _Pane.ScrollToBottom(); return true;
                default: return false;
            }
        }

        /// <summary>
        /// Wheel scrolling.
        /// </summary>
        /// <param name="mouse">Mouse event.</param>
        /// <returns>True when handled.</returns>
        public bool HandleMouse(MouseEvent mouse)
        {
            return _Pane.HandleMouse(mouse);
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
        /// Render the pane.
        /// </summary>
        /// <param name="surface">Surface.</param>
        public void Render(ISurface surface)
        {
            _Pane.Render(surface);
        }

        #endregion
    }
}

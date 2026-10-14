namespace Xeno.Tui.Modals
{
    using System;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Modals;
    using TUIKit.Widgets;

    /// <summary>
    /// Holds the menu bar while a menu is open, so its drop-down gets every key and click before the page underneath:
    /// a click on a title switches menus, a click on an item runs it, and a click anywhere else closes the menu.
    /// Closes itself as soon as the menu bar closes.
    /// </summary>
    internal sealed class MenuBarModal : Modal
    {
        #region Public-Members

        /// <summary>
        /// Menu bar shown.
        /// </summary>
        public MenuBar Bar { get; }

        /// <summary>
        /// Screen row of the menu bar.
        /// </summary>
        public int Row { get; }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Hold an open menu bar.
        /// </summary>
        /// <param name="bar">Menu bar.</param>
        /// <param name="row">Screen row of the menu bar.</param>
        /// <exception cref="ArgumentNullException">Bar is null.</exception>
        public MenuBarModal(MenuBar bar, int row)
        {
            Bar = bar ?? throw new ArgumentNullException(nameof(bar));
            Row = Math.Max(0, row);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Draw the menu bar and its open drop-down.
        /// </summary>
        /// <param name="surface">Screen surface.</param>
        public override void Render(ISurface surface)
        {
            if (surface.Size.Height <= Row + 1) return;
            Bar.Render(new SurfaceView(surface, new Rect(0, Row, surface.Size.Width, surface.Size.Height - Row)));
        }

        /// <summary>
        /// Route a key to the menu bar.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <returns>True.</returns>
        public override bool HandleKey(KeyEvent key)
        {
            if (key.Code == KeyCode.F10) Bar.Close();
            else Bar.HandleKey(key);
            CloseWhenDone();
            return true;
        }

        /// <summary>
        /// Route a click to the menu bar; a click above it closes the menu.
        /// </summary>
        /// <param name="mouse">Mouse event in screen coordinates.</param>
        /// <returns>True.</returns>
        public override bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null) return true;
            if (mouse.Y < Row)
            {
                if (mouse.Kind == MouseEventKind.Press) Bar.Close();
            }
            else
            {
                Bar.HandleMouse(new MouseEvent(mouse.Kind, mouse.Button, mouse.X, mouse.Y - Row, mouse.Modifiers, mouse.ClickCount));
            }

            CloseWhenDone();
            return true;
        }

        #endregion

        #region Private-Methods

        private void CloseWhenDone()
        {
            if (!Bar.IsOpen && !IsClosed) Close(null);
        }

        #endregion
    }
}

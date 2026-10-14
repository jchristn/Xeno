namespace Xeno.Tui.Shell
{
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Widgets;

    /// <summary>
    /// Hosts the TUIKit menu bar in the one-row menu region without joining the focus ring. Keys reach the menu bar
    /// through the application key filter while it is open (F10 opens it); mouse presses on menu titles open it
    /// directly. The open drop-down is drawn by the render overlay so it can extend below the region.
    /// </summary>
    internal sealed class MenuBarHost : IWidget, IMouseAware
    {
        #region Public-Members

        /// <summary>
        /// Hosted menu bar.
        /// </summary>
        public MenuBar Bar { get; set; } = new MenuBar();

        /// <summary>
        /// Called after a click opens a menu.
        /// </summary>
        public System.Action? Opened { get; set; } = null;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Measure.
        /// </summary>
        /// <param name="available">Available size.</param>
        /// <returns>Desired size.</returns>
        public Size Measure(Size available)
        {
            return new Size(available.Width, 1);
        }

        /// <summary>
        /// Render the closed bar. The open drop-down is drawn by the overlay.
        /// </summary>
        /// <param name="surface">Surface.</param>
        public void Render(ISurface surface)
        {
            Bar.Render(surface);
        }

        /// <summary>
        /// Forward mouse events to the menu bar.
        /// </summary>
        /// <param name="mouse">Mouse event.</param>
        /// <returns>True when handled.</returns>
        public bool HandleMouse(MouseEvent mouse)
        {
            bool handled = Bar.HandleMouse(mouse);
            if (handled && Bar.IsOpen) Opened?.Invoke();
            return handled;
        }

        #endregion
    }
}

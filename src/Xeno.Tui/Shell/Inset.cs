namespace Xeno.Tui.Shell
{
    using System;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Theming;
    using TUIKit.Widgets;

    /// <summary>
    /// Keeps one empty column between a framed pane's borders and its content, on the left and the right, so text,
    /// tables, charts, and buttons never touch a frame. Wraps a widget that does not take focus; use
    /// <see cref="Around(IWidget)"/> to pick this or <see cref="FocusInset"/> to match the child.
    /// </summary>
    internal sealed class Inset : IWidget, IThemeable, IMouseAware, ITooltipProvider
    {
        #region Public-Members

        /// <summary>
        /// Columns kept empty on each side.
        /// </summary>
        public const int Columns = 1;

        /// <summary>
        /// Wrapped widget.
        /// </summary>
        public IWidget Child { get; }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Wrap a widget that does not take focus.
        /// </summary>
        /// <param name="child">Child.</param>
        /// <exception cref="ArgumentNullException">Child is null.</exception>
        public Inset(IWidget child)
        {
            Child = child ?? throw new ArgumentNullException(nameof(child));
        }

        /// <summary>
        /// Wrap a widget in the inset that matches it: <see cref="FocusInset"/> for focusable widgets and containers,
        /// otherwise <see cref="Inset"/>. Framed stacks are returned as they are, because their own children are
        /// inset where they are added.
        /// </summary>
        /// <param name="child">Child.</param>
        /// <returns>The wrapped widget.</returns>
        /// <exception cref="ArgumentNullException">Child is null.</exception>
        public static IWidget Around(IWidget child)
        {
            ArgumentNullException.ThrowIfNull(child);
            if (child is FramedStack || child is Inset || child is FocusInset) return child;
            if (child is IFocusable) return new FocusInset(child);
            return new Inset(child);
        }

        /// <summary>
        /// The content rectangle inside an inset of a given size.
        /// </summary>
        /// <param name="size">Outer size.</param>
        /// <returns>Content rectangle.</returns>
        public static Rect ContentRect(Size size)
        {
            return new Rect(Columns, 0, Math.Max(0, size.Width - Columns * 2), size.Height);
        }

        /// <summary>
        /// Translate a mouse event into content coordinates, or null when it falls in the padding.
        /// </summary>
        /// <param name="mouse">Mouse event in inset coordinates.</param>
        /// <returns>The event in content coordinates, or null.</returns>
        public static MouseEvent? ToContent(MouseEvent mouse)
        {
            if (mouse == null || mouse.X < Columns) return null;
            return new MouseEvent(mouse.Kind, mouse.Button, mouse.X - Columns, mouse.Y, mouse.Modifiers, mouse.ClickCount);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Measure the child in the narrower width and add the padding back.
        /// </summary>
        /// <param name="available">Available size.</param>
        /// <returns>Desired size.</returns>
        public Size Measure(Size available)
        {
            Size inner = Child.Measure(new Size(Math.Max(0, available.Width - Columns * 2), available.Height));
            return new Size(Math.Min(available.Width, inner.Width + Columns * 2), inner.Height);
        }

        /// <summary>
        /// Render the child inside the padding.
        /// </summary>
        /// <param name="surface">Surface.</param>
        public void Render(ISurface surface)
        {
            Rect content = ContentRect(surface.Size);
            if (content.Width <= 0) return;
            Child.Render(new SurfaceView(surface, content));
        }

        /// <summary>
        /// Apply a theme to the child.
        /// </summary>
        /// <param name="theme">Theme.</param>
        public void ApplyTheme(Theme theme)
        {
            if (Child is IThemeable themeable) themeable.ApplyTheme(theme);
        }

        /// <summary>
        /// Forward a mouse event to the child.
        /// </summary>
        /// <param name="mouse">Mouse event.</param>
        /// <returns>True when handled.</returns>
        public bool HandleMouse(MouseEvent mouse)
        {
            MouseEvent? inner = ToContent(mouse);
            return inner != null && Child is IMouseAware aware && aware.HandleMouse(inner);
        }

        /// <summary>
        /// Forward a tooltip query to the child.
        /// </summary>
        /// <param name="x">Column.</param>
        /// <param name="y">Row.</param>
        /// <returns>Tooltip, or null.</returns>
        public string? GetTooltip(int x, int y)
        {
            return x >= Columns && Child is ITooltipProvider provider ? provider.GetTooltip(x - Columns, y) : null;
        }

        #endregion
    }
}

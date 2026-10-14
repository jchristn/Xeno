namespace Xeno.Tui.Shell
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Theming;
    using TUIKit.Widgets;

    /// <summary>
    /// The focusable counterpart of <see cref="Inset"/>: keeps one empty column on each side of a table, list, button
    /// row, dropdown, or log, and forwards keys, focus, mouse, key hints, and theme to it.
    /// </summary>
    internal sealed class FocusInset : IWidget, IFocusContainer, IFocusAware, IFocusPathNode, IMouseAware, IThemeable, IKeyHintSource, IHideable, ITooltipProvider
    {
        #region Public-Members

        /// <summary>
        /// Wrapped widget.
        /// </summary>
        public IWidget Child { get; }

        /// <summary>
        /// Focused leaf: the child's own focused leaf, or the child itself.
        /// </summary>
        public IFocusable? FocusedLeaf
        {
            get
            {
                if (Child is IFocusContainer container) return container.FocusedLeaf;
                return Child as IFocusable;
            }
        }

        /// <summary>
        /// Focused child one level down: the child.
        /// </summary>
        public IFocusable? FocusedChild
        {
            get { return Child as IFocusable; }
        }

        /// <summary>
        /// True when the child has something to interact with.
        /// </summary>
        public bool IsVisible
        {
            get
            {
                if (Child is IHideable hideable && !hideable.IsVisible) return false;
                return !(Child is IEnableable enableable) || enableable.IsEnabled;
            }
        }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Wrap a focusable widget.
        /// </summary>
        /// <param name="child">Child.</param>
        /// <exception cref="ArgumentNullException">Child is null.</exception>
        /// <exception cref="ArgumentException">Child does not take focus.</exception>
        public FocusInset(IWidget child)
        {
            Child = child ?? throw new ArgumentNullException(nameof(child));
            if (!(child is IFocusable)) throw new ArgumentException("The child must take focus.", nameof(child));
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
            Size inner = Child.Measure(new Size(Math.Max(0, available.Width - Inset.Columns * 2), available.Height));
            return new Size(Math.Min(available.Width, inner.Width + Inset.Columns * 2), inner.Height);
        }

        /// <summary>
        /// Render the child inside the padding.
        /// </summary>
        /// <param name="surface">Surface.</param>
        public void Render(ISurface surface)
        {
            Rect content = Inset.ContentRect(surface.Size);
            if (content.Width <= 0) return;
            Child.Render(new SurfaceView(surface, content));
        }

        /// <summary>
        /// Forward a key to the child.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <returns>True when handled.</returns>
        public bool HandleKey(KeyEvent key)
        {
            return Child is IFocusable focusable && focusable.HandleKey(key);
        }

        /// <summary>
        /// Move focus within the child.
        /// </summary>
        /// <param name="forward">Direction.</param>
        /// <returns>True when focus moved within the child.</returns>
        public bool MoveFocus(bool forward)
        {
            return Child is IFocusContainer container && container.MoveFocus(forward);
        }

        /// <summary>
        /// Focus the child's first or last stop.
        /// </summary>
        /// <param name="first">True for the first stop.</param>
        public void FocusEdge(bool first)
        {
            if (Child is IFocusContainer container) container.FocusEdge(first);
        }

        /// <summary>
        /// Forward a focus change to the child.
        /// </summary>
        /// <param name="focused">True when focused.</param>
        public void OnFocusChanged(bool focused)
        {
            if (Child is IFocusAware aware) aware.OnFocusChanged(focused);
        }

        /// <summary>
        /// Forward a mouse event to the child.
        /// </summary>
        /// <param name="mouse">Mouse event.</param>
        /// <returns>True when handled.</returns>
        public bool HandleMouse(MouseEvent mouse)
        {
            MouseEvent? inner = Inset.ToContent(mouse);
            return inner != null && Child is IMouseAware aware && aware.HandleMouse(inner);
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
        /// The child's key hints.
        /// </summary>
        /// <returns>Hints, or null.</returns>
        public IReadOnlyList<KeyHint>? GetKeyHints()
        {
            return Child is IKeyHintSource source ? source.GetKeyHints() : null;
        }

        /// <summary>
        /// Forward a tooltip query to the child.
        /// </summary>
        /// <param name="x">Column.</param>
        /// <param name="y">Row.</param>
        /// <returns>Tooltip, or null.</returns>
        public string? GetTooltip(int x, int y)
        {
            return x >= Inset.Columns && Child is ITooltipProvider provider ? provider.GetTooltip(x - Inset.Columns, y) : null;
        }

        #endregion
    }
}

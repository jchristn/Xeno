namespace Xeno.Tui.Shell
{
    using System;
    using TUIKit;
    using TUIKit.Widgets;

    /// <summary>
    /// Finds where a widget was last drawn inside a page's widget tree, through framed stacks and insets.
    /// </summary>
    internal static class WidgetLocator
    {
        #region Public-Methods

        /// <summary>
        /// The rectangle a widget was last drawn in, or null when it is not in the tree or was not laid out.
        /// </summary>
        /// <param name="node">Tree root.</param>
        /// <param name="rect">Rectangle the root was drawn in.</param>
        /// <param name="target">Widget to find.</param>
        /// <returns>Rectangle in the same coordinates as <paramref name="rect"/>, or null.</returns>
        public static Rect? Locate(IWidget node, Rect rect, IWidget target)
        {
            if (ReferenceEquals(node, target)) return rect;
            if (node is FramedStack stack)
            {
                foreach (IWidget child in stack.Children)
                {
                    Rect content = stack.ContentRectOf(child);
                    if (content.Width <= 0 || content.Height <= 0) continue;
                    Rect? found = Locate(child, new Rect(rect.X + content.X, rect.Y + content.Y, content.Width, content.Height), target);
                    if (found != null) return found;
                }

                return null;
            }

            IWidget? inner = node is Inset inset ? inset.Child : node is FocusInset focusInset ? focusInset.Child : null;
            if (inner == null) return null;
            return Locate(inner, new Rect(rect.X + Inset.Columns, rect.Y, Math.Max(0, rect.Width - Inset.Columns * 2), rect.Height), target);
        }

        #endregion
    }
}

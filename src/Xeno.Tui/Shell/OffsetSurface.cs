namespace Xeno.Tui.Shell
{
    using System;
    using TUIKit;

    /// <summary>
    /// A rectangle of another surface, so a widget can draw into part of a pane: coordinates are shifted by the origin
    /// and anything outside the rectangle is dropped.
    /// </summary>
    internal sealed class OffsetSurface : ISurface
    {
        #region Private-Members

        private readonly ISurface _Inner;
        private readonly int _Left;
        private readonly int _Top;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the view.
        /// </summary>
        /// <param name="inner">Surface to draw into.</param>
        /// <param name="left">Left edge in the inner surface.</param>
        /// <param name="top">Top edge in the inner surface.</param>
        /// <param name="width">Width.</param>
        /// <param name="height">Height.</param>
        public OffsetSurface(ISurface inner, int left, int top, int width, int height)
        {
            _Inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _Left = left;
            _Top = top;
            Size = new Size(Math.Max(0, width), Math.Max(0, height));
        }

        #endregion

        #region Public-Members

        /// <inheritdoc />
        public Size Size { get; }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public void Set(int x, int y, Cell cell)
        {
            if (x < 0 || y < 0 || x >= Size.Width || y >= Size.Height) return;
            _Inner.Set(_Left + x, _Top + y, cell);
        }

        /// <inheritdoc />
        public void Fill(Rect region, Cell cell)
        {
            int left = Math.Max(0, region.X);
            int top = Math.Max(0, region.Y);
            int right = Math.Min(Size.Width, region.X + region.Width);
            int bottom = Math.Min(Size.Height, region.Y + region.Height);
            if (right <= left || bottom <= top) return;
            _Inner.Fill(new Rect(_Left + left, _Top + top, right - left, bottom - top), cell);
        }

        #endregion
    }
}

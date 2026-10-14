namespace Xeno.Tui.Shell
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Unicode;
    using TUIKit.Widgets;

    /// <summary>
    /// Shows the status bar and makes its key hints clickable: a click on a hint reports the hint, and the app runs
    /// that key as if it were pressed.
    /// </summary>
    internal sealed class StatusBarHost : IWidget, IMouseAware
    {
        #region Public-Members

        /// <summary>
        /// Status bar shown.
        /// </summary>
        public StatusBar Bar { get; }

        /// <summary>
        /// Called when a hint is clicked.
        /// </summary>
        public Action<KeyHint>? HintClicked { get; set; } = null;

        #endregion

        #region Private-Members

        private const string HintGap = "   ";
        private int _Width = 0;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Host a status bar.
        /// </summary>
        /// <param name="bar">Status bar.</param>
        /// <exception cref="ArgumentNullException">Bar is null.</exception>
        public StatusBarHost(StatusBar bar)
        {
            Bar = bar ?? throw new ArgumentNullException(nameof(bar));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Measure: one row.
        /// </summary>
        /// <param name="available">Available size.</param>
        /// <returns>Desired size.</returns>
        public Size Measure(Size available)
        {
            return Bar.Measure(available);
        }

        /// <summary>
        /// Render the status bar.
        /// </summary>
        /// <param name="surface">Surface.</param>
        public void Render(ISurface surface)
        {
            _Width = surface.Size.Width;
            Bar.Render(surface);
        }

        /// <summary>
        /// Report a click on a hint.
        /// </summary>
        /// <param name="mouse">Mouse event.</param>
        /// <returns>True when a hint was clicked.</returns>
        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null || mouse.Kind != MouseEventKind.Press || mouse.Button != MouseButton.Left) return false;
            KeyHint? hint = HintAt(mouse.X);
            if (hint == null) return false;
            HintClicked?.Invoke(hint);
            return true;
        }

        /// <summary>
        /// The hint drawn at a column, laid out the way <see cref="StatusBar"/> draws them.
        /// </summary>
        /// <param name="x">Column.</param>
        /// <returns>Hint, or null.</returns>
        public KeyHint? HintAt(int x)
        {
            int width = _Width > 0 ? _Width : Int32.MaxValue;
            int cursor = 0;
            KeyHint? reserved = Bar.ReservedHint;
            if (reserved != null)
            {
                int end = cursor + Width(reserved);
                if (x >= cursor && x < end) return reserved;
                cursor = end + HintGap.Length;
            }

            IReadOnlyList<KeyHint>? hints = Bar.HintSource?.Invoke();
            if (hints == null) return null;
            foreach (KeyHint? hint in hints)
            {
                if (cursor >= width) break;
                if (hint == null) continue;
                if (reserved != null && String.Equals(hint.Key, reserved.Key, StringComparison.Ordinal)) continue;
                int end = cursor + Width(hint);
                if (end > width) break;
                if (x >= cursor && x < end) return hint;
                cursor = end + HintGap.Length;
            }

            return null;
        }

        #endregion

        #region Private-Methods

        private static int Width(KeyHint hint)
        {
            return TextFit.Width(hint.Key) + 1 + TextFit.Width(hint.Description);
        }

        #endregion
    }
}

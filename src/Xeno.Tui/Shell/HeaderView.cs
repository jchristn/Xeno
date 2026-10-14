namespace Xeno.Tui.Shell
{
    using System;
    using TUIKit;
    using TUIKit.Theming;
    using TUIKit.Widgets;
    using Xeno.Client.Hosting;
    using Xeno.Client.Services;

    /// <summary>
    /// One-line header: product, client status, client name, Xeno server, and model count.
    /// </summary>
    internal sealed class HeaderView : IWidget, IThemeable
    {
        #region Public-Members

        /// <summary>
        /// Status to show, or null while loading.
        /// </summary>
        public ClientStatusSnapshot? Status { get; set; } = null;

        /// <summary>
        /// True while a client operation runs.
        /// </summary>
        public bool Busy { get; set; } = false;

        #endregion

        #region Private-Members

        private Theme _Theme = XenoTuiTheme.Dark;

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
        /// Render the header.
        /// </summary>
        /// <param name="surface">Surface.</param>
        public void Render(ISurface surface)
        {
            int width = surface.Size.Width;
            if (width <= 0 || surface.Size.Height <= 0) return;
            surface.Fill(new Rect(0, 0, width, 1), Cell.Blank(_Theme.Selection));
            int x = surface.DrawText(1, 0, " " + XenoProduct.Name + " ", _Theme.Selection.WithAttribute(CellAttributes.Bold, true)) + 1;
            if (Status == null)
            {
                surface.DrawText(x + 1, 0, "checking...", _Theme.Selection);
                return;
            }

            string dot = Status.Status == ClientStatusEnum.Connected ? "●" : Status.Running ? "◐" : "○";
            string state = Busy ? "Working..." : Status.StatusText;
            x += surface.DrawText(x + 1, 0, dot + " " + state, _Theme.Selection.WithAttribute(CellAttributes.Bold, true)) + 2;
            string detail = DisplayFormat.OrDash(Status.Name) + "  •  " + DisplayFormat.OrDash(Status.ServerLabel) + "  •  " + Status.ModelCount + " model" + (Status.ModelCount == 1 ? String.Empty : "s");
            int room = width - x - 2;
            if (room > 4) surface.DrawText(x + 1, 0, detail.Length > room ? detail.Substring(0, room) : detail, _Theme.Selection);
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
    }
}

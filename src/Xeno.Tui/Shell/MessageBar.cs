namespace Xeno.Tui.Shell
{
    using System;
    using System.Globalization;
    using TUIKit;
    using TUIKit.Modals;
    using TUIKit.Theming;
    using TUIKit.Widgets;

    /// <summary>
    /// The message line above the key hints: what the app is doing (with a spinner) or what the last action did,
    /// colored by outcome and stamped with the local time, so every button and command visibly reports back.
    /// </summary>
    internal sealed class MessageBar : IWidget, IThemeable
    {
        #region Private-Members

        private static readonly string[] SpinnerFrames = new[] { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };
        private readonly TuiContext _Context;
        private Theme _Theme = XenoTuiTheme.Dark;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the message line over the shared context's message.
        /// </summary>
        /// <param name="context">Context.</param>
        /// <exception cref="ArgumentNullException">Context is null.</exception>
        public MessageBar(TuiContext context)
        {
            _Context = context ?? throw new ArgumentNullException(nameof(context));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Marker shown before a message: a spinner frame while working, otherwise a symbol for the severity.
        /// </summary>
        /// <param name="severity">Severity.</param>
        /// <param name="working">True while working.</param>
        /// <returns>Marker.</returns>
        public static string Marker(NotificationSeverity severity, bool working)
        {
            if (working) return SpinnerFrames[(int)((Environment.TickCount64 / 100) % SpinnerFrames.Length)];
            switch (severity)
            {
                case NotificationSeverity.Success: return "✓";
                case NotificationSeverity.Warning: return "!";
                case NotificationSeverity.Error: return "✗";
                default: return "•";
            }
        }

        /// <summary>
        /// Measure: one row.
        /// </summary>
        /// <param name="available">Available size.</param>
        /// <returns>Desired size.</returns>
        public Size Measure(Size available)
        {
            return new Size(available.Width, Math.Min(1, available.Height));
        }

        /// <summary>
        /// Render the message.
        /// </summary>
        /// <param name="surface">Surface.</param>
        public void Render(ISurface surface)
        {
            int width = surface.Size.Width;
            string message = _Context.Message;
            NotificationSeverity severity = _Context.MessageSeverity;
            bool working = _Context.Working;
            DateTime? messageUtc = _Context.MessageUtc;
            if (width <= 2 || surface.Size.Height <= 0 || String.IsNullOrWhiteSpace(message)) return;
            CellStyle style = working ? _Theme.Accent : Style(severity);
            string time = messageUtc == null ? String.Empty : messageUtc.Value.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "  ";
            string marker = Marker(severity, working) + " ";
            int x = 1;
            surface.DrawText(x, 0, marker, style.WithAttribute(CellAttributes.Bold, true));
            x += marker.Length;
            if (time.Length > 0 && x + time.Length < width)
            {
                surface.DrawText(x, 0, time, _Theme.Muted);
                x += time.Length;
            }

            int room = width - x - 1;
            if (room <= 0) return;
            string text = message.Replace(Environment.NewLine, " ", StringComparison.Ordinal).Replace('\n', ' ');
            if (text.Length > room) text = room <= 1 ? text.Substring(0, room) : text.Substring(0, room - 1) + "…";
            surface.DrawText(x, 0, text, severity == NotificationSeverity.Info && !working ? _Theme.Text : style);
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

        private CellStyle Style(NotificationSeverity severity)
        {
            switch (severity)
            {
                case NotificationSeverity.Success: return _Theme.Success;
                case NotificationSeverity.Warning: return _Theme.Warning;
                case NotificationSeverity.Error: return _Theme.Error;
                default: return _Theme.Info;
            }
        }

        #endregion
    }
}

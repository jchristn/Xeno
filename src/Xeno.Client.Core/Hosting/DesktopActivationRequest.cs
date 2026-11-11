namespace Xeno.Client.Hosting
{
    using System;
    using Xeno.Client.Presentation;

    /// <summary>
    /// A request for the running desktop app to come to the front, written by another process (the tray agent, or a
    /// second xeno-desktop launch) and picked up by the running instance through <see cref="DesktopActivation"/>.
    /// </summary>
    public sealed class DesktopActivationRequest
    {
        /// <summary>
        /// Page to show, or null to keep the current page.
        /// </summary>
        public XenoPageEnum? Page { get; set; } = null;

        /// <summary>
        /// True to open the Settings window.
        /// </summary>
        public bool OpenSettings { get; set; } = false;

        /// <summary>
        /// When the request was written, UTC.
        /// </summary>
        public DateTime RequestedUtc { get; set; } = DateTime.UtcNow;
    }
}

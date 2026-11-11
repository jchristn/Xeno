namespace Xeno.Client.Hosting
{
    using System;

    /// <summary>
    /// A request, written by the tray agent when it quits, for every other Xeno app on this client home to quit.
    /// </summary>
    public sealed class XenoQuitRequest
    {
        /// <summary>
        /// When the request was made, UTC.
        /// </summary>
        public DateTime RequestedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Process that asked.
        /// </summary>
        public int ProcessId { get; set; } = 0;
    }
}

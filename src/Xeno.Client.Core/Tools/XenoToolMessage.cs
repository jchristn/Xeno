namespace Xeno.Client.Tools
{
    using System;

    /// <summary>
    /// Simple result: what a tool did.
    /// </summary>
    public sealed class XenoToolMessage
    {
        /// <summary>
        /// True when the action succeeded.
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// What happened, in words.
        /// </summary>
        public string Message { get; set; } = String.Empty;
    }
}

namespace Xeno.Core.Settings
{
    using System;
    using System.Collections.Generic;
    using Xeno.Core.Enums;

    /// <summary>
    /// Routing settings.
    /// </summary>
    public class RoutingSettings
    {
        /// <summary>
        /// Default timeout in milliseconds.
        /// </summary>
        public int DefaultTimeoutMs { get; set; } = 300000;
    }
}

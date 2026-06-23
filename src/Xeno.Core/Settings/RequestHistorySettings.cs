namespace Xeno.Core.Settings
{
    using System;
    using System.Collections.Generic;
    using Xeno.Core.Enums;

    /// <summary>
    /// Request history settings.
    /// </summary>
    public class RequestHistorySettings
    {
        /// <summary>
        /// Enable request capture.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Retention in days.
        /// </summary>
        public int RetentionDays { get; set; } = 30;
    }
}

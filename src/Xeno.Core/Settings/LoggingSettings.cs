namespace Xeno.Core.Settings
{
    using System;
    using System.Collections.Generic;
    using Xeno.Core.Enums;

    /// <summary>
    /// Logging settings.
    /// </summary>
    public class LoggingSettings
    {
        /// <summary>
        /// Enable console logging.
        /// </summary>
        public bool ConsoleLogging { get; set; } = true;

        /// <summary>
        /// Enable file logging.
        /// </summary>
        public bool FileLogging { get; set; } = true;

        /// <summary>
        /// Log directory.
        /// </summary>
        public string LogDirectory { get; set; } = "logs";

        /// <summary>
        /// Log filename.
        /// </summary>
        public string LogFilename { get; set; } = "xeno.log";
    }
}

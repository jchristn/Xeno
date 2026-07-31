namespace Xeno.Core.Settings
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json.Serialization;

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
        /// Enable colors in console output.
        /// </summary>
        public bool EnableColors { get; set; } = false;

        /// <summary>
        /// Minimum severity to emit, 0 (Debug) through 7 (Emergency).
        /// Default is 0.
        /// </summary>
        public int MinimumSeverity
        {
            get => _MinimumSeverity;
            set
            {
                if (value < 0 || value > 7) throw new ArgumentOutOfRangeException(nameof(MinimumSeverity));
                _MinimumSeverity = value;
            }
        }

        /// <summary>
        /// Enable file logging.
        /// </summary>
        public bool FileLogging { get; set; } = true;

        /// <summary>
        /// Log directory.
        /// </summary>
        public string LogDirectory { get; set; } = "./logs/";

        /// <summary>
        /// Log filename.
        /// </summary>
        public string LogFilename { get; set; } = "xeno.log";

        /// <summary>
        /// Include the date in the log filename, producing one file per day.
        /// </summary>
        public bool IncludeDateInFilename { get; set; } = true;

        /// <summary>
        /// Effective log filename, combining <see cref="LogDirectory"/> and <see cref="LogFilename"/>.
        /// Returns just the filename when the directory is null or empty, and an empty string when no
        /// filename is configured. Derived from other settings; not serialized.
        /// </summary>
        [JsonIgnore]
        public string Filename
        {
            get
            {
                if (String.IsNullOrEmpty(LogFilename)) return String.Empty;
                if (String.IsNullOrEmpty(LogDirectory)) return LogFilename;
                return Path.Combine(LogDirectory, LogFilename);
            }
        }

        /// <summary>
        /// Syslog servers to which log messages should also be sent.
        /// Empty by default; syslog output is opt-in.
        /// </summary>
        public List<SyslogServer> Servers
        {
            get => _Servers;
            set => _Servers = value ?? new List<SyslogServer>();
        }

        private int _MinimumSeverity = 0;
        private List<SyslogServer> _Servers = new List<SyslogServer>();

        /// <summary>
        /// Instantiate.
        /// </summary>
        public LoggingSettings()
        {
        }
    }
}

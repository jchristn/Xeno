namespace Xeno.DataLoader
{
    using System;
    using Xeno.Core.Enums;

    internal sealed class DataLoaderOptions
    {
        private int _ClientCount = 12;
        private double _Randomness = 0.65;
        private int _TelemetrySamplesPerHour = 12;

        public DateTime FromUtc { get; set; } = DateTime.UtcNow.AddDays(-31);

        public DateTime ToUtc { get; set; } = DateTime.UtcNow;

        public int ClientCount
        {
            get { return _ClientCount; }
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(value), "Client count must be at least 1.");
                _ClientCount = value;
            }
        }

        public double Randomness
        {
            get { return _Randomness; }
            set
            {
                if (value < 0 || value > 1) throw new ArgumentOutOfRangeException(nameof(value), "Randomness must be between 0 and 1.");
                _Randomness = value;
            }
        }

        public int TelemetrySamplesPerHour
        {
            get { return _TelemetrySamplesPerHour; }
            set
            {
                if (value < 0 || value > 60) throw new ArgumentOutOfRangeException(nameof(value), "Telemetry density must be between 0 and 60 samples per hour.");
                _TelemetrySamplesPerHour = value;
            }
        }

        public DatabaseTypeEnum DatabaseType { get; set; } = DatabaseTypeEnum.Sqlite;

        public string? Filename { get; set; } = "docker/data/xeno.db";

        public string? ConnectionString { get; set; } = null;

        public string TenantId { get; set; } = "ten_default";

        public string Scenario { get; set; } = "screenshot";

        public int? Seed { get; set; } = null;

        public bool ReplaceSynthetic { get; set; } = true;

        public bool Backup { get; set; } = true;

        public bool ShowHelp { get; set; } = false;
    }
}

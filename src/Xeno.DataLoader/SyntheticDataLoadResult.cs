namespace Xeno.DataLoader
{
    using System;

    internal sealed class SyntheticDataLoadResult
    {
        public DateTime FromUtc { get; set; }

        public DateTime ToUtc { get; set; }

        public int ClientsCreated { get; set; }

        public int RequestHistoryEntriesCreated { get; set; }

        public int HostTelemetrySamplesCreated { get; set; }

        public int ProviderTelemetrySamplesCreated { get; set; }

        public int CollectionAttemptsCreated { get; set; }

        public string? BackupPath { get; set; }
    }
}

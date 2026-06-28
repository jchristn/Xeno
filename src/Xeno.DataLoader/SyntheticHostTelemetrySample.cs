namespace Xeno.DataLoader
{
    using System;

    internal sealed class SyntheticHostTelemetrySample
    {
        public string Id { get; set; } = string.Empty;

        public string CollectionRequestId { get; set; } = string.Empty;

        public SyntheticClientProfile Client { get; set; } = new SyntheticClientProfile();

        public DateTime CollectedUtc { get; set; }

        public DateTime ReceivedUtc { get; set; }

        public DateTime PersistedUtc { get; set; }

        public double CollectionElapsedMs { get; set; }

        public double SampleAgeMsAtReceive { get; set; }

        public double CpuPercent { get; set; }

        public double MemoryPercent { get; set; }

        public double DiskPercent { get; set; }

        public double GpuPercent { get; set; }

        public string SnapshotJson { get; set; } = string.Empty;
    }
}

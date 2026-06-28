namespace Xeno.DataLoader
{
    using System;

    internal sealed class SyntheticProviderTelemetrySample
    {
        public string Id { get; set; } = string.Empty;

        public SyntheticClientProfile Client { get; set; } = new SyntheticClientProfile();

        public DateTime CollectedUtc { get; set; }

        public DateTime ReceivedUtc { get; set; }

        public DateTime PersistedUtc { get; set; }

        public string Status { get; set; } = "Available";

        public string HealthStatus { get; set; } = "Healthy";

        public double ActiveRequests { get; set; }

        public double QueuedRequests { get; set; }

        public double LatencyP95Ms { get; set; }

        public int AvailableModelCount { get; set; }

        public int LoadedModelCount { get; set; }

        public string SnapshotJson { get; set; } = string.Empty;
    }
}

namespace Xeno.DataLoader
{
    using System;

    internal sealed class SyntheticCollectionAttempt
    {
        public string Id { get; set; } = string.Empty;

        public SyntheticClientProfile Client { get; set; } = new SyntheticClientProfile();

        public DateTime StartedUtc { get; set; }

        public DateTime CompletedUtc { get; set; }

        public double ElapsedMs { get; set; }

        public string HostSampleId { get; set; } = string.Empty;

        public int ProviderSampleCount { get; set; } = 1;
    }
}

namespace Xeno.DataLoader
{
    using System.Collections.Generic;

    internal sealed class SyntheticTelemetryBatch
    {
        public List<SyntheticHostTelemetrySample> HostSamples { get; set; } = new List<SyntheticHostTelemetrySample>();

        public List<SyntheticProviderTelemetrySample> ProviderSamples { get; set; } = new List<SyntheticProviderTelemetrySample>();

        public List<SyntheticCollectionAttempt> Attempts { get; set; } = new List<SyntheticCollectionAttempt>();
    }
}

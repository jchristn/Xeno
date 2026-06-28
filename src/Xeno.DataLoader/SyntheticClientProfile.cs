namespace Xeno.DataLoader
{
    using System.Collections.Generic;

    internal sealed class SyntheticClientProfile
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string MachineName { get; set; } = string.Empty;

        public string Zone { get; set; } = string.Empty;

        public string ProviderName { get; set; } = string.Empty;

        public string ProviderKind { get; set; } = string.Empty;

        public string ApiType { get; set; } = string.Empty;

        public string Source { get; set; } = string.Empty;

        public string EndpointDisplay { get; set; } = string.Empty;

        public string EndpointHash { get; set; } = string.Empty;

        public string GpuName { get; set; } = string.Empty;

        public int GpuCount { get; set; }

        public int ProcessorCount { get; set; }

        public int MemoryGb { get; set; }

        public int DiskTb { get; set; }

        public int Weight { get; set; }

        public int MaxParallelRequests { get; set; }

        public double BaseCpuPercent { get; set; }

        public double BaseMemoryPercent { get; set; }

        public double BaseDiskPercent { get; set; }

        public double BaseGpuPercent { get; set; }

        public double BaseLatencyMs { get; set; }

        public double Phase { get; set; }

        public List<SyntheticModelProfile> Models { get; set; } = new List<SyntheticModelProfile>();
    }
}

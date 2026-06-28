namespace Xeno.DataLoader
{
    internal sealed class SyntheticClientTemplate
    {
        public string Location { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;

        public string Zone { get; set; } = string.Empty;

        public string ProviderName { get; set; } = string.Empty;

        public string ProviderKind { get; set; } = string.Empty;

        public string ApiType { get; set; } = string.Empty;

        public string GpuName { get; set; } = string.Empty;

        public int MinimumGpuCount { get; set; }

        public int MaximumGpuCount { get; set; }

        public int ProcessorCount { get; set; }

        public int MemoryGb { get; set; }

        public int DiskTb { get; set; }

        public double BaseLatencyMs { get; set; }

        public double Weight { get; set; } = 1;
    }
}

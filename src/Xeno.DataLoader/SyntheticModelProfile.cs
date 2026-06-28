namespace Xeno.DataLoader
{
    internal sealed class SyntheticModelProfile
    {
        public string Name { get; set; } = string.Empty;

        public bool SupportsCompletions { get; set; }

        public bool SupportsEmbeddings { get; set; }

        public double Weight { get; set; } = 1;

        public double SizeFactor { get; set; } = 1;
    }
}

namespace Xeno.DataLoader
{
    using System.Collections.Generic;

    internal static class SyntheticModelCatalog
    {
        public static List<SyntheticModelProfile> CreateCompletionModels()
        {
            return new List<SyntheticModelProfile>
            {
                new SyntheticModelProfile { Name = "llama3.2:latest", SupportsCompletions = true, SupportsEmbeddings = false, Weight = 12, SizeFactor = 0.75 },
                new SyntheticModelProfile { Name = "llama3.1:8b", SupportsCompletions = true, SupportsEmbeddings = false, Weight = 10, SizeFactor = 0.7 },
                new SyntheticModelProfile { Name = "mistral-nemo:latest", SupportsCompletions = true, SupportsEmbeddings = false, Weight = 9, SizeFactor = 0.95 },
                new SyntheticModelProfile { Name = "qwen2.5:14b", SupportsCompletions = true, SupportsEmbeddings = false, Weight = 8, SizeFactor = 1.2 },
                new SyntheticModelProfile { Name = "qwen2.5:32b", SupportsCompletions = true, SupportsEmbeddings = false, Weight = 5, SizeFactor = 1.7 },
                new SyntheticModelProfile { Name = "llama3.1:70b", SupportsCompletions = true, SupportsEmbeddings = false, Weight = 3, SizeFactor = 2.35 },
                new SyntheticModelProfile { Name = "gpt-oss:120b", SupportsCompletions = true, SupportsEmbeddings = false, Weight = 2, SizeFactor = 3.1 }
            };
        }

        public static List<SyntheticModelProfile> CreateEmbeddingModels()
        {
            return new List<SyntheticModelProfile>
            {
                new SyntheticModelProfile { Name = "nomic-embed-text", SupportsCompletions = false, SupportsEmbeddings = true, Weight = 7, SizeFactor = 0.25 },
                new SyntheticModelProfile { Name = "bge-m3", SupportsCompletions = false, SupportsEmbeddings = true, Weight = 5, SizeFactor = 0.32 },
                new SyntheticModelProfile { Name = "all-minilm", SupportsCompletions = false, SupportsEmbeddings = true, Weight = 4, SizeFactor = 0.18 }
            };
        }
    }
}

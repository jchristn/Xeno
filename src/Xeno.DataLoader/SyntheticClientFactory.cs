namespace Xeno.DataLoader
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Security.Cryptography;
    using System.Text;

    internal sealed class SyntheticClientFactory
    {
        private readonly DataLoaderOptions _Options;
        private readonly RandomSource _Random;

        public SyntheticClientFactory(DataLoaderOptions options, RandomSource random)
        {
            _Options = options ?? throw new ArgumentNullException(nameof(options));
            _Random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public List<SyntheticClientProfile> CreateClients()
        {
            List<SyntheticClientTemplate> templates = CreateTemplates();
            List<WeightedChoice<SyntheticClientTemplate>> weightedTemplates = new List<WeightedChoice<SyntheticClientTemplate>>();
            foreach (SyntheticClientTemplate template in templates)
            {
                weightedTemplates.Add(new WeightedChoice<SyntheticClientTemplate>(template, template.Weight));
            }

            List<SyntheticClientProfile> clients = new List<SyntheticClientProfile>();
            for (int index = 0; index < _Options.ClientCount; index++)
            {
                SyntheticClientTemplate template = _Random.PickWeighted(weightedTemplates);
                SyntheticClientProfile client = CreateClient(template, index);
                clients.Add(client);
            }

            return clients;
        }

        private SyntheticClientProfile CreateClient(SyntheticClientTemplate template, int index)
        {
            string ordinal = (index + 1).ToString("00", CultureInfo.InvariantCulture);
            string normalizedLocation = template.Location.ToLowerInvariant();
            string normalizedRole = template.Role.ToLowerInvariant();
            int gpuCount = _Random.NextInt32(template.MinimumGpuCount, template.MaximumGpuCount + 1);
            string clientName = normalizedLocation + "-" + normalizedRole + "-" + ShortGpu(template.GpuName) + "-" + ordinal;
            string id = SyntheticIdFactory.Create(SyntheticConstants.ClientIdPrefix, clientName + "-" + (_Options.Seed ?? 0).ToString(CultureInfo.InvariantCulture), 32);
            string endpoint = CreateEndpoint(template, index);
            List<SyntheticModelProfile> models = CreateModels(template, gpuCount);

            return new SyntheticClientProfile
            {
                Id = id,
                Name = clientName,
                MachineName = template.Location.ToUpperInvariant() + "-" + normalizedRole.ToUpperInvariant() + "-" + ShortGpu(template.GpuName).ToUpperInvariant() + "-" + ordinal,
                Zone = template.Zone,
                ProviderName = template.ProviderName,
                ProviderKind = template.ProviderKind,
                ApiType = template.ApiType,
                Source = template.ProviderName.ToLowerInvariant().Replace(" ", "-") + "-" + normalizedLocation,
                EndpointDisplay = endpoint,
                EndpointHash = CreateEndpointHash(endpoint),
                GpuName = template.GpuName,
                GpuCount = gpuCount,
                ProcessorCount = template.ProcessorCount + _Random.NextInt32(-4, 5),
                MemoryGb = template.MemoryGb,
                DiskTb = template.DiskTb,
                Weight = Math.Max(25, (int)Math.Round((template.Weight * 35) + (gpuCount * 18))),
                MaxParallelRequests = Math.Max(2, gpuCount * 2 + _Random.NextInt32(0, 3)),
                BaseCpuPercent = RandomSource.Clamp(_Random.Gaussian(36 + gpuCount * 2, 8 + _Options.Randomness * 10), 8, 78),
                BaseMemoryPercent = RandomSource.Clamp(_Random.Gaussian(52 + gpuCount * 2, 6 + _Options.Randomness * 8), 20, 88),
                BaseDiskPercent = RandomSource.Clamp(_Random.Gaussian(45 + index % 9 * 4, 5), 20, 86),
                BaseGpuPercent = RandomSource.Clamp(_Random.Gaussian(58 + gpuCount * 2, 11 + _Options.Randomness * 14), 10, 94),
                BaseLatencyMs = template.BaseLatencyMs * _Random.Gaussian(1.0, 0.12 + _Options.Randomness * 0.22),
                Phase = _Random.NextDouble() * Math.PI * 2,
                Models = models
            };
        }

        private List<SyntheticModelProfile> CreateModels(SyntheticClientTemplate template, int gpuCount)
        {
            List<SyntheticModelProfile> completions = SyntheticModelCatalog.CreateCompletionModels();
            List<SyntheticModelProfile> embeddings = SyntheticModelCatalog.CreateEmbeddingModels();
            List<SyntheticModelProfile> result = new List<SyntheticModelProfile>();
            int completionCount = Math.Min(completions.Count, Math.Max(2, gpuCount + _Random.NextInt32(1, 4)));
            for (int index = 0; index < completionCount; index++)
            {
                SyntheticModelProfile model = _Random.Pick(completions);
                if (!ContainsModel(result, model.Name)) result.Add(model);
            }

            if (template.ProviderKind == "Ollama" || _Random.Chance(0.65))
            {
                SyntheticModelProfile embedding = _Random.Pick(embeddings);
                if (!ContainsModel(result, embedding.Name)) result.Add(embedding);
            }

            return result;
        }

        private static bool ContainsModel(List<SyntheticModelProfile> models, string name)
        {
            foreach (SyntheticModelProfile model in models)
            {
                if (String.Equals(model.Name, name, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }

        private static string CreateEndpoint(SyntheticClientTemplate template, int index)
        {
            int third = 10 + index % 180;
            int fourth = 20 + (index * 13) % 180;
            int port = template.ProviderKind == "Ollama" ? 11434 : template.ProviderKind == "LmStudio" ? 1234 : 8000;
            return "http://10." + (40 + index % 40).ToString(CultureInfo.InvariantCulture) + "." + third.ToString(CultureInfo.InvariantCulture) + "." + fourth.ToString(CultureInfo.InvariantCulture) + ":" + port.ToString(CultureInfo.InvariantCulture);
        }

        private static string ShortGpu(string gpuName)
        {
            if (gpuName.Contains("H100", StringComparison.OrdinalIgnoreCase)) return "h100";
            if (gpuName.Contains("L40S", StringComparison.OrdinalIgnoreCase)) return "l40s";
            if (gpuName.Contains("A6000", StringComparison.OrdinalIgnoreCase)) return "a6000";
            if (gpuName.Contains("6000 Ada", StringComparison.OrdinalIgnoreCase)) return "rtx6000";
            if (gpuName.Contains("4090", StringComparison.OrdinalIgnoreCase)) return "4090";
            if (gpuName.Contains("3090", StringComparison.OrdinalIgnoreCase)) return "3090";
            return "gpu";
        }

        private static string CreateEndpointHash(string endpoint)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(endpoint));
            StringBuilder builder = new StringBuilder(16);
            for (int index = 0; index < 8; index++)
            {
                builder.Append(hash[index].ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static List<SyntheticClientTemplate> CreateTemplates()
        {
            return new List<SyntheticClientTemplate>
            {
                new SyntheticClientTemplate { Location = "austin", Role = "lab", Zone = "us-central", ProviderName = "Ollama", ProviderKind = "Ollama", ApiType = "Ollama", GpuName = "NVIDIA GeForce RTX 4090", MinimumGpuCount = 1, MaximumGpuCount = 2, ProcessorCount = 32, MemoryGb = 128, DiskTb = 4, BaseLatencyMs = 1180, Weight = 5 },
                new SyntheticClientTemplate { Location = "seattle", Role = "edge", Zone = "us-west", ProviderName = "vLLM", ProviderKind = "Vllm", ApiType = "OpenAICompatible", GpuName = "NVIDIA RTX A6000", MinimumGpuCount = 2, MaximumGpuCount = 4, ProcessorCount = 48, MemoryGb = 256, DiskTb = 8, BaseLatencyMs = 920, Weight = 6 },
                new SyntheticClientTemplate { Location = "newark", Role = "rack", Zone = "us-east", ProviderName = "vLLM", ProviderKind = "Vllm", ApiType = "OpenAICompatible", GpuName = "NVIDIA H100 PCIe", MinimumGpuCount = 4, MaximumGpuCount = 8, ProcessorCount = 96, MemoryGb = 512, DiskTb = 14, BaseLatencyMs = 680, Weight = 4 },
                new SyntheticClientTemplate { Location = "berlin", Role = "studio", Zone = "eu-central", ProviderName = "Ollama", ProviderKind = "Ollama", ApiType = "Ollama", GpuName = "NVIDIA GeForce RTX 3090", MinimumGpuCount = 1, MaximumGpuCount = 2, ProcessorCount = 24, MemoryGb = 96, DiskTb = 3, BaseLatencyMs = 1580, Weight = 3 },
                new SyntheticClientTemplate { Location = "tokyo", Role = "lab", Zone = "ap-northeast", ProviderName = "vLLM", ProviderKind = "Vllm", ApiType = "OpenAICompatible", GpuName = "NVIDIA L40S", MinimumGpuCount = 2, MaximumGpuCount = 4, ProcessorCount = 64, MemoryGb = 384, DiskTb = 10, BaseLatencyMs = 1050, Weight = 4 },
                new SyntheticClientTemplate { Location = "denver", Role = "colo", Zone = "us-mountain", ProviderName = "LM Studio", ProviderKind = "LmStudio", ApiType = "OpenAICompatible", GpuName = "NVIDIA RTX 6000 Ada", MinimumGpuCount = 1, MaximumGpuCount = 4, ProcessorCount = 40, MemoryGb = 192, DiskTb = 6, BaseLatencyMs = 1320, Weight = 3 },
                new SyntheticClientTemplate { Location = "toronto", Role = "render", Zone = "ca-central", ProviderName = "Ollama", ProviderKind = "Ollama", ApiType = "Ollama", GpuName = "NVIDIA GeForce RTX 4090", MinimumGpuCount = 1, MaximumGpuCount = 2, ProcessorCount = 32, MemoryGb = 128, DiskTb = 5, BaseLatencyMs = 1260, Weight = 4 },
                new SyntheticClientTemplate { Location = "london", Role = "rack", Zone = "eu-west", ProviderName = "Text Generation Inference", ProviderKind = "TextGenerationInference", ApiType = "OpenAICompatible", GpuName = "NVIDIA L40S", MinimumGpuCount = 2, MaximumGpuCount = 6, ProcessorCount = 72, MemoryGb = 384, DiskTb = 12, BaseLatencyMs = 980, Weight = 3 }
            };
        }
    }
}

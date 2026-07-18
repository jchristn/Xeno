namespace Xeno.Client
{
    using System.Text.Json.Serialization;

    internal sealed class OllamaModelPullProgress
    {
        [JsonPropertyName("status")]
        public string? Status { get; set; } = null;

        [JsonPropertyName("digest")]
        public string? Digest { get; set; } = null;

        [JsonPropertyName("total")]
        public long? Total { get; set; } = null;

        [JsonPropertyName("completed")]
        public long? Completed { get; set; } = null;
    }
}

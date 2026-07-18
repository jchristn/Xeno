namespace Xeno.Client
{
    using System.Text.Json.Serialization;

    internal sealed class OllamaModelPullRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("stream")]
        public bool Stream { get; set; } = true;

        [JsonPropertyName("insecure")]
        public bool Insecure { get; set; } = false;
    }
}

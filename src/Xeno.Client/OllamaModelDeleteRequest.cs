namespace Xeno.Client
{
    using System.Text.Json.Serialization;

    internal sealed class OllamaModelDeleteRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;
    }
}

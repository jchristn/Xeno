namespace Xeno.Client.Chat
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// The capabilities part of an Ollama /api/show response ("completion", "tools", "thinking", "embedding", ...).
    /// </summary>
    public sealed class OllamaShowCapabilitiesDto
    {
        /// <summary>Capabilities, or null on older Ollama versions.</summary>
        [JsonPropertyName("capabilities")]
        public List<string>? Capabilities { get; set; } = null;

        /// <summary>The model's prompt template, or null.</summary>
        [JsonPropertyName("template")]
        public string? Template { get; set; } = null;

        /// <summary>The built-in prompt renderer newer Ollama models use in place of the template, or null.</summary>
        [JsonPropertyName("renderer")]
        public string? Renderer { get; set; } = null;

        /// <summary>
        /// True when the model can use tools: Ollama lists the "tools" capability and the prompt template renders
        /// tools (a built-in renderer, a Go template using .Tools, or a Jinja template using tools). Some templates (deepseek-r1 builds, for one) are listed with "tools" but drop them silently.
        /// </summary>
        [JsonIgnore]
        public bool UsesTools
        {
            get
            {
                bool listed = Capabilities != null && Capabilities.Exists(item => System.String.Equals(item, "tools", System.StringComparison.OrdinalIgnoreCase));
                if (!listed) return false;
                if (!System.String.IsNullOrWhiteSpace(Renderer) || Template == null) return true;
                return Template.Contains(".Tools", System.StringComparison.Ordinal) || Template.Contains("tools", System.StringComparison.Ordinal);
            }
        }
    }
}

namespace Xeno.Client.Telemetry.Models
{
    /// <summary>
    /// Provider telemetry kind.
    /// </summary>
    public enum ClientProviderTelemetryKindEnum
    {
        /// <summary>
        /// Unknown provider kind.
        /// </summary>
        Unknown,

        /// <summary>
        /// Ollama provider.
        /// </summary>
        Ollama,

        /// <summary>
        /// vLLM provider.
        /// </summary>
        Vllm,

        /// <summary>
        /// Generic OpenAI-compatible provider.
        /// </summary>
        OpenAICompatible,

        /// <summary>
        /// OpenAI cloud provider.
        /// </summary>
        OpenAI,

        /// <summary>
        /// Gemini cloud provider.
        /// </summary>
        Gemini,

        /// <summary>
        /// LocalAI provider.
        /// </summary>
        LocalAI,

        /// <summary>
        /// Text Generation Inference provider.
        /// </summary>
        TextGenerationInference,

        /// <summary>
        /// llama.cpp compatible provider.
        /// </summary>
        LlamaCpp,

        /// <summary>
        /// LM Studio compatible provider.
        /// </summary>
        LmStudio
    }
}

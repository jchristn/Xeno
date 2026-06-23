namespace Xeno.Core.Enums
{

    /// <summary>
    /// Model provider API family.
    /// </summary>
    public enum ApiProviderTypeEnum
    {
        /// <summary>
        /// Ollama-native provider.
        /// </summary>
        Ollama = 0,

        /// <summary>
        /// OpenAI provider.
        /// </summary>
        OpenAI = 1,

        /// <summary>
        /// Gemini provider.
        /// </summary>
        Gemini = 2,

        /// <summary>
        /// vLLM OpenAI-compatible provider.
        /// </summary>
        vLLM = 3,

        /// <summary>
        /// Generic OpenAI-compatible provider.
        /// </summary>
        OpenAICompatible = 4
    }
}

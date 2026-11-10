namespace Xeno.Client.Routing
{
    /// <summary>
    /// API shape a local app used when calling the local inference listener.
    /// </summary>
    public enum LocalInferenceApiShapeEnum
    {
        /// <summary>
        /// OpenAI-compatible routes under /v1.
        /// </summary>
        OpenAi,

        /// <summary>
        /// Ollama-compatible routes under /api.
        /// </summary>
        Ollama
    }
}

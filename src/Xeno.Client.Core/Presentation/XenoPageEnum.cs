namespace Xeno.Client.Presentation
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Pages shared by the desktop app and the terminal app. Both front ends render the same set, in the same order.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum XenoPageEnum
    {
        /// <summary>
        /// Client status, quick statistics, and actions.
        /// </summary>
        Overview,

        /// <summary>
        /// Unified catalog of models served by this machine and offered by the Xeno network.
        /// </summary>
        Models,

        /// <summary>
        /// Local provider endpoints (Ollama, OpenAI-compatible, vLLM) and the models each serves.
        /// </summary>
        Providers,

        /// <summary>
        /// Local endpoint: the URL local apps call, where each model goes, and provider health and load.
        /// </summary>
        LocalEndpoint,

        /// <summary>
        /// Requests this machine served for the Xeno network.
        /// </summary>
        Requests,

        /// <summary>
        /// Worker log.
        /// </summary>
        Logs,

        /// <summary>
        /// Host telemetry: CPU, memory, disk, GPU.
        /// </summary>
        HostTelemetry,

        /// <summary>
        /// Provider telemetry: health, loaded models, queue depth.
        /// </summary>
        ProviderTelemetry,

        /// <summary>
        /// Xeno server connection and account.
        /// </summary>
        Server,

        /// <summary>
        /// Local dashboard, local inference server, history, and telemetry settings.
        /// </summary>
        Settings,

        /// <summary>
        /// Chat with a local or Xeno network model.
        /// </summary>
        Chat,

        /// <summary>
        /// Chat request history.
        /// </summary>
        ChatHistory,

        /// <summary>
        /// Ratings and comments on chat answers.
        /// </summary>
        ChatFeedback,

        /// <summary>
        /// Usage analytics: latency, tokens, groups, heatmap, and timing by stage for served requests, the local
        /// endpoint, and chat.
        /// </summary>
        Usage
    }
}

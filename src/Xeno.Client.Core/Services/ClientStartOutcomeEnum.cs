namespace Xeno.Client.Services
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Outcome of starting the client worker.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ClientStartOutcomeEnum
    {
        /// <summary>
        /// A new worker started and registered with the Xeno server.
        /// </summary>
        Started,

        /// <summary>
        /// A registered worker with fresh activity was already running.
        /// </summary>
        AlreadyRunning,

        /// <summary>
        /// The worker started but did not register before the timeout, or exited.
        /// </summary>
        NotRegistered
    }
}

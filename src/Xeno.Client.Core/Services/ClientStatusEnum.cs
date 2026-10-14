namespace Xeno.Client.Services
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Overall state of this machine's Xeno client, as shown by every front end.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ClientStatusEnum
    {
        /// <summary>
        /// No server credential is configured; the client cannot connect until the user signs in.
        /// </summary>
        NotSignedIn,

        /// <summary>
        /// No worker is running.
        /// </summary>
        Stopped,

        /// <summary>
        /// A worker is running but has not registered with the Xeno server yet.
        /// </summary>
        Starting,

        /// <summary>
        /// A worker is running and registered with the Xeno server.
        /// </summary>
        Connected,

        /// <summary>
        /// A worker is running but lost its server connection and is reconnecting.
        /// </summary>
        Reconnecting
    }
}

namespace Xeno.Core.Analytics
{
    /// <summary>
    /// Stable code for one stage of a request or chat turn. Clients translate the code; it is never shown raw.
    /// </summary>
    public enum UsageStageEnum
    {
        /// <summary>
        /// Time spent in the Xeno server outside the relay round trip.
        /// </summary>
        ServerOverhead,

        /// <summary>
        /// Time between the server sending to a client and the provider starting, including the client.
        /// </summary>
        RelayAndClient,

        /// <summary>
        /// Time waiting for a provider concurrency slot.
        /// </summary>
        Queue,

        /// <summary>
        /// Time until the provider returned response headers.
        /// </summary>
        ProviderHeaders,

        /// <summary>
        /// Time from response headers (or the request) until the first token or byte.
        /// </summary>
        ProviderFirstToken,

        /// <summary>
        /// Time from the first token until the response finished.
        /// </summary>
        Streaming,

        /// <summary>
        /// Chat: time until the model wrote its first token.
        /// </summary>
        ModelFirstToken,

        /// <summary>
        /// Chat: time the model spent writing after its first token.
        /// </summary>
        ModelStreaming,

        /// <summary>
        /// Chat: time tools ran.
        /// </summary>
        Tools,

        /// <summary>
        /// Chat: time tool calls waited for Allow or Deny.
        /// </summary>
        Approval,

        /// <summary>
        /// Chat: the rest of the turn.
        /// </summary>
        Other
    }
}

namespace Xeno.Client.Routing
{
    using System;

    /// <summary>
    /// Point-in-time concurrency counters for one provider.
    /// </summary>
    public class ProviderConcurrencySnapshot
    {
        /// <summary>
        /// Provider name. Never null.
        /// </summary>
        public string Provider { get; set; } = String.Empty;

        /// <summary>
        /// Requests holding a slot, local and network combined.
        /// </summary>
        public int InFlight { get; set; } = 0;

        /// <summary>
        /// Local listener requests holding a slot.
        /// </summary>
        public int InFlightLocal { get; set; } = 0;

        /// <summary>
        /// Network requests holding a slot.
        /// </summary>
        public int InFlightNetwork { get; set; } = 0;

        /// <summary>
        /// Local requests waiting for a slot.
        /// </summary>
        public int Queued { get; set; } = 0;

        /// <summary>
        /// Provider limit currently in effect.
        /// </summary>
        public int Limit { get; set; } = 0;

        /// <summary>
        /// Local requests rejected with 429 because the wait timed out, since the client started.
        /// </summary>
        public long LocalRejected { get; set; } = 0;

        /// <summary>
        /// Network requests turned away because the provider was full, since the client started.
        /// </summary>
        public long NetworkRejected { get; set; } = 0;
    }
}

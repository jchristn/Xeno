namespace Xeno.Client.Routing
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Mutable counters for one provider inside <see cref="ProviderConcurrencyGate"/>. Only touched under the gate's lock.
    /// </summary>
    internal sealed class ProviderConcurrencyState
    {
        internal int InFlightLocal;

        internal int InFlightNetwork;

        internal int Queued;

        internal long LocalRejected;

        internal long NetworkRejected;

        internal Dictionary<string, int> Models { get; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    }
}

namespace Xeno.Client.Routing
{
    using System;
    using System.Threading;

    /// <summary>
    /// A held concurrency slot. Dispose it when the request finishes to release the slot. Disposing twice has no effect.
    /// </summary>
    public sealed class ConcurrencyLease : IDisposable
    {
        private readonly ProviderConcurrencyGate _Gate;
        private int _Released;

        internal ConcurrencyLease(ProviderConcurrencyGate gate, string provider, string model, bool local, double waitedMs)
        {
            _Gate = gate;
            Provider = provider;
            Model = model;
            Local = local;
            WaitedMs = waitedMs;
        }

        /// <summary>
        /// Provider name. Never null.
        /// </summary>
        public string Provider { get; }

        /// <summary>
        /// Normalized model name, or empty when the request has no model. Never null.
        /// </summary>
        public string Model { get; }

        /// <summary>
        /// True for a local listener request, false for a network request.
        /// </summary>
        public bool Local { get; }

        /// <summary>
        /// Milliseconds the request waited for the slot.
        /// </summary>
        public double WaitedMs { get; }

        /// <summary>
        /// Release the slot.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _Released, 1) == 1) return;
            _Gate.Release(this);
        }
    }
}

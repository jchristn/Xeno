namespace Xeno.Server
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Database.Interfaces;
    using Xeno.Core.Models;

    /// <summary>
    /// One in-flight slot a relayed request holds on a client. The slot is released as soon as the client's final
    /// response arrives, before request history and ledger bookkeeping, so a caller that sends its next request right
    /// after reading a response never finds the client's slots still held by finished work. Release is idempotent.
    /// </summary>
    internal sealed class ClientInFlightSlot
    {
        private readonly IClientMethods _Clients;
        private readonly XenoClientRecord _Client;
        private int _Released = 0;

        /// <summary>
        /// Wrap a slot already counted on the client.
        /// </summary>
        /// <param name="clients">Client database methods.</param>
        /// <param name="client">Client holding the slot.</param>
        /// <exception cref="ArgumentNullException">clients or client is null.</exception>
        public ClientInFlightSlot(IClientMethods clients, XenoClientRecord client)
        {
            _Clients = clients ?? throw new ArgumentNullException(nameof(clients));
            _Client = client ?? throw new ArgumentNullException(nameof(client));
        }

        /// <summary>
        /// Release the slot once. Later calls do nothing. The database update is not cancellable, so the counter never
        /// leaks when the request is cancelled.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task ReleaseAsync()
        {
            if (Interlocked.Exchange(ref _Released, 1) != 0) return;
            _Client.InFlightRequests = Math.Max(0, _Client.InFlightRequests - 1);
            await _Clients.AdjustInFlightAsync(_Client.TenantId, _Client.Id, -1, CancellationToken.None).ConfigureAwait(false);
        }
    }
}

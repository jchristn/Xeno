namespace Xeno.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Enums;
    using Xeno.Core.Models;
    using Xeno.Core.Settings;

    /// <summary>
    /// Drains queued ledger events to NetLedger.
    /// </summary>
    public class LedgerDrainService
    {
        #region Private-Members

        private readonly LedgerSettings _Settings;
        private readonly NetLedgerClient _Client;
        private readonly LedgerAccountResolver _Resolver;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the drain service.
        /// </summary>
        /// <param name="settings">Ledger settings.</param>
        /// <param name="client">NetLedger client.</param>
        /// <param name="resolver">Account resolver.</param>
        public LedgerDrainService(LedgerSettings settings, NetLedgerClient client, LedgerAccountResolver resolver)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Client = client ?? throw new ArgumentNullException(nameof(client));
            _Resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Process one event.
        /// </summary>
        /// <param name="ledgerEvent">Ledger event.</param>
        /// <param name="token">Cancellation token.</param>
        public async Task ProcessAsync(LedgerEvent ledgerEvent, CancellationToken token = default)
        {
            if (ledgerEvent == null) throw new ArgumentNullException(nameof(ledgerEvent));
            token.ThrowIfCancellationRequested();

            LedgerTenantLink tenantLink = await _Resolver.ResolveTenantAsync(ledgerEvent.XenoTenantId, token).ConfigureAwait(false);
            LedgerAccountLink debitAccount = await _Resolver.ResolveAccountAsync(ledgerEvent.XenoTenantId, ledgerEvent.RequestUserId, tenantLink, token).ConfigureAwait(false);
            LedgerAccountLink creditAccount = await _Resolver.ResolveAccountAsync(ledgerEvent.XenoTenantId, ledgerEvent.ServingUserId, tenantLink, token).ConfigureAwait(false);

            await WriteEntryIfMissingAsync(tenantLink.NetLedgerTenantId, debitAccount.NetLedgerAccountId, ledgerEvent, LedgerEntryDirection.Debit, token).ConfigureAwait(false);
            await WriteEntryIfMissingAsync(tenantLink.NetLedgerTenantId, creditAccount.NetLedgerAccountId, ledgerEvent, LedgerEntryDirection.Credit, token).ConfigureAwait(false);
        }

        #endregion

        #region Private-Methods

        private async Task WriteEntryIfMissingAsync(string netLedgerTenantId, string netLedgerAccountId, LedgerEvent ledgerEvent, LedgerEntryDirection direction, CancellationToken token)
        {
            Dictionary<string, string> tags = LedgerMetadataBuilder.BuildEntryTags(ledgerEvent, direction);
            List<NetLedgerEntry> existing = await _Client.EnumerateEntriesByTagsAsync(netLedgerTenantId, netLedgerAccountId, tags, token).ConfigureAwait(false);
            if (existing.Count > 0) return;

            await _Client.AddEntryAsync(netLedgerTenantId, netLedgerAccountId, direction, new NetLedgerCreateEntryRequest
            {
                Amount = ledgerEvent.TotalTokens,
                Notes = BuildEntryNotes(ledgerEvent, direction),
                IsCommitted = _Settings.CommitEntriesImmediately,
                Labels = LedgerMetadataBuilder.BuildEntryLabels(direction),
                Tags = tags
            }, token).ConfigureAwait(false);
        }

        private static string BuildEntryNotes(LedgerEvent ledgerEvent, LedgerEntryDirection direction)
        {
            string action = direction == LedgerEntryDirection.Debit ? "debit" : "credit";
            return "Xeno " + action + " for request " + ledgerEvent.RequestHistoryId;
        }

        #endregion
    }
}

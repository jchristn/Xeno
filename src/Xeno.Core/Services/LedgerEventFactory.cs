namespace Xeno.Core.Services
{
    using System;
    using Xeno.Core.Enums;
    using Xeno.Core.Models;

    /// <summary>
    /// Builds ledger events from request history.
    /// </summary>
    public static class LedgerEventFactory
    {
        /// <summary>
        /// Try to build a ledger event.
        /// </summary>
        /// <param name="platform">Platform state.</param>
        /// <param name="history">Request history entry.</param>
        /// <param name="ledgerEvent">Ledger event when one can be built.</param>
        /// <param name="reason">Reason no event was built.</param>
        /// <returns>True if an event was built.</returns>
        public static bool TryCreate(XenoPlatform platform, RequestHistoryEntry history, out LedgerEvent? ledgerEvent, out string? reason)
        {
            if (platform == null) throw new ArgumentNullException(nameof(platform));
            if (history == null) throw new ArgumentNullException(nameof(history));

            ledgerEvent = null;
            reason = null;

            if (history.StatusCode < 200 || history.StatusCode >= 300)
            {
                reason = "Request did not complete successfully.";
                return false;
            }

            int totalTokens = Math.Max(0, history.Telemetry?.TotalTokens ?? 0);
            if (totalTokens <= 0)
            {
                reason = "Request history entry does not include positive total token usage.";
                return false;
            }

            if (String.IsNullOrWhiteSpace(history.SelectedClientId))
            {
                reason = "Request history entry does not include a selected client.";
                return false;
            }

            string? requestUserId = ResolveRequestUserId(platform, history);
            if (String.IsNullOrWhiteSpace(requestUserId))
            {
                reason = "Requesting user could not be resolved.";
                return false;
            }

            XenoClientRecord? selectedClient = platform.Clients.Find(item =>
                String.Equals(item.Id, history.SelectedClientId, StringComparison.OrdinalIgnoreCase)
                && String.Equals(item.TenantId, history.TenantId, StringComparison.OrdinalIgnoreCase));
            if (selectedClient == null || String.IsNullOrWhiteSpace(selectedClient.OwnerUserId))
            {
                reason = "Serving client owner could not be resolved.";
                return false;
            }

            ledgerEvent = new LedgerEvent
            {
                XenoTenantId = history.TenantId,
                RequestHistoryId = history.Id,
                RequestCredentialId = history.CredentialId,
                RequestUserId = requestUserId,
                SelectedClientId = selectedClient.Id,
                ServingUserId = selectedClient.OwnerUserId,
                RequestKind = IsEmbedding(history) ? LedgerRequestKind.Embedding : LedgerRequestKind.Completion,
                TotalTokens = totalTokens,
                OccurredUtc = (history.CompletedUtc ?? history.CreatedUtc).ToUniversalTime()
            };
            return true;
        }

        private static string? ResolveRequestUserId(XenoPlatform platform, RequestHistoryEntry history)
        {
            if (!String.IsNullOrWhiteSpace(history.CredentialId))
            {
                Credential? credential = platform.Credentials.Find(item =>
                    String.Equals(item.Id, history.CredentialId, StringComparison.OrdinalIgnoreCase)
                    && String.Equals(item.TenantId, history.RequestorTenantId, StringComparison.OrdinalIgnoreCase));
                if (!String.IsNullOrWhiteSpace(credential?.UserId)) return credential.UserId;
            }

            if (!String.IsNullOrWhiteSpace(history.UserId)) return history.UserId;
            if (history.RequestorPrincipalType == PrincipalTypeEnum.User && !String.IsNullOrWhiteSpace(history.RequestorPrincipalId)) return history.RequestorPrincipalId;
            return null;
        }

        private static bool IsEmbedding(RequestHistoryEntry history)
        {
            if (history.Path.Contains("embedding", StringComparison.OrdinalIgnoreCase)) return true;
            if (history.Path.Contains("embed", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}

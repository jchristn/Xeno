namespace Xeno.Core.Services
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Database;
    using Xeno.Core.Enums;
    using Xeno.Core.Models;

    /// <summary>
    /// Outcome of a ledger event creation attempt.
    /// </summary>
    public class LedgerEventCreationResult
    {
        /// <summary>
        /// True when a ledger event was built.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Ledger event when one could be built; otherwise null.
        /// </summary>
        public LedgerEvent? Event { get; set; }

        /// <summary>
        /// Reason no event was built; otherwise null.
        /// </summary>
        public string? Reason { get; set; }
    }

    /// <summary>
    /// Builds ledger events from request history.
    /// </summary>
    public static class LedgerEventFactory
    {
        /// <summary>
        /// Try to build a ledger event.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="history">Request history entry.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Ledger event creation result.</returns>
        public static async Task<LedgerEventCreationResult> TryCreateAsync(DatabaseDriverBase database, RequestHistoryEntry history, CancellationToken token = default)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (history == null) throw new ArgumentNullException(nameof(history));

            if (history.StatusCode < 200 || history.StatusCode >= 300)
            {
                return Failure("Request did not complete successfully.");
            }

            int totalTokens = Math.Max(0, history.Telemetry?.TotalTokens ?? 0);
            if (totalTokens <= 0)
            {
                return Failure("Request history entry does not include positive total token usage.");
            }

            if (String.IsNullOrWhiteSpace(history.SelectedClientId))
            {
                return Failure("Request history entry does not include a selected client.");
            }

            string? requestUserId = await ResolveRequestUserIdAsync(database, history, token).ConfigureAwait(false);
            if (String.IsNullOrWhiteSpace(requestUserId))
            {
                return Failure("Requesting user could not be resolved.");
            }

            XenoClientRecord? selectedClient = await database.Clients!.ReadAsync(history.TenantId, history.SelectedClientId, token).ConfigureAwait(false);
            if (selectedClient == null || String.IsNullOrWhiteSpace(selectedClient.OwnerUserId))
            {
                return Failure("Serving client owner could not be resolved.");
            }

            LedgerEvent ledgerEvent = new LedgerEvent
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

            return new LedgerEventCreationResult
            {
                Success = true,
                Event = ledgerEvent,
                Reason = null
            };
        }

        private static LedgerEventCreationResult Failure(string reason)
        {
            return new LedgerEventCreationResult
            {
                Success = false,
                Event = null,
                Reason = reason
            };
        }

        private static async Task<string?> ResolveRequestUserIdAsync(DatabaseDriverBase database, RequestHistoryEntry history, CancellationToken token)
        {
            if (!String.IsNullOrWhiteSpace(history.CredentialId))
            {
                Credential? credential = await database.Credentials!.ReadAsync(history.RequestorTenantId, history.CredentialId, token).ConfigureAwait(false);
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

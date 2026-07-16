namespace Xeno.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using Xeno.Core.Enums;
    using Xeno.Core.Models;

    /// <summary>
    /// Builds NetLedger labels and tags for Xeno ledger records.
    /// </summary>
    public static class LedgerMetadataBuilder
    {
        /// <summary>
        /// Build account labels.
        /// </summary>
        /// <returns>Account labels.</returns>
        public static List<string> BuildAccountLabels()
        {
            return new List<string> { "xeno", "xeno-user" };
        }

        /// <summary>
        /// Build account tags.
        /// </summary>
        /// <param name="tenantId">Xeno tenant identifier.</param>
        /// <param name="user">Xeno user.</param>
        /// <returns>Account tags.</returns>
        public static Dictionary<string, string> BuildAccountTags(string tenantId, User user)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
            if (user == null) throw new ArgumentNullException(nameof(user));

            Dictionary<string, string> tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["source"] = "xeno",
                ["xenoTenantId"] = tenantId,
                ["xenoUserId"] = user.Id
            };

            if (!String.IsNullOrWhiteSpace(user.Email))
            {
                tags["xenoUserName"] = user.Email;
            }
            else if (!String.IsNullOrWhiteSpace(user.Username))
            {
                tags["xenoUserName"] = user.Username;
            }

            return tags;
        }

        /// <summary>
        /// Build entry labels.
        /// </summary>
        /// <param name="direction">Entry direction.</param>
        /// <returns>Entry labels.</returns>
        public static List<string> BuildEntryLabels(LedgerEntryDirection direction)
        {
            return new List<string>
            {
                "xeno",
                "xeno-token-usage",
                direction == LedgerEntryDirection.Debit ? "xeno-debit" : "xeno-credit"
            };
        }

        /// <summary>
        /// Build entry tags.
        /// </summary>
        /// <param name="ledgerEvent">Ledger event.</param>
        /// <param name="direction">Entry direction.</param>
        /// <returns>Entry tags.</returns>
        public static Dictionary<string, string> BuildEntryTags(LedgerEvent ledgerEvent, LedgerEntryDirection direction)
        {
            if (ledgerEvent == null) throw new ArgumentNullException(nameof(ledgerEvent));

            Dictionary<string, string> tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["source"] = "xeno",
                ["ledgerEventId"] = ledgerEvent.Id,
                ["direction"] = direction == LedgerEntryDirection.Debit ? "debit" : "credit",
                ["xenoTenantId"] = ledgerEvent.XenoTenantId,
                ["requestHistoryId"] = ledgerEvent.RequestHistoryId,
                ["requestUserId"] = ledgerEvent.RequestUserId,
                ["selectedClientId"] = ledgerEvent.SelectedClientId,
                ["servingUserId"] = ledgerEvent.ServingUserId,
                ["counterpartyUserId"] = direction == LedgerEntryDirection.Debit ? ledgerEvent.ServingUserId : ledgerEvent.RequestUserId,
                ["requestKind"] = ledgerEvent.RequestKind == LedgerRequestKind.Embedding ? "embedding" : "completion",
                ["occurredUtc"] = ledgerEvent.OccurredUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)
            };

            if (!String.IsNullOrWhiteSpace(ledgerEvent.RequestCredentialId))
            {
                tags["requestCredentialId"] = ledgerEvent.RequestCredentialId;
            }

            return tags;
        }
    }
}

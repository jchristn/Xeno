namespace Xeno.Core.Helpers
{
    using System;
    using System.Collections.Generic;
    using Xeno.Core.Models;

    /// <summary>
    /// Applies user-reference anonymization to a request history entry, matching the platform semantics.
    /// </summary>
    public static class RequestHistoryAnonymizer
    {
        /// <summary>
        /// Null out references to the specified user, credentials, and sessions on the entry.
        /// </summary>
        /// <param name="entry">Entry to anonymize in place.</param>
        /// <param name="userId">User identifier being anonymized.</param>
        /// <param name="credentialIds">Credential identifiers owned by the user.</param>
        /// <param name="sessionIds">Session identifiers owned by the user.</param>
        public static void Anonymize(RequestHistoryEntry entry, string userId, IReadOnlyCollection<string>? credentialIds, IReadOnlyCollection<string>? sessionIds)
        {
            if (entry == null) return;
            if (!String.IsNullOrEmpty(userId))
            {
                if (String.Equals(entry.UserId, userId, StringComparison.OrdinalIgnoreCase)) entry.UserId = null;
                if (String.Equals(entry.RequestorPrincipalId, userId, StringComparison.OrdinalIgnoreCase)) entry.RequestorPrincipalId = null;
            }

            if (!String.IsNullOrWhiteSpace(entry.CredentialId) && Contains(credentialIds, entry.CredentialId)) entry.CredentialId = null;
            if (!String.IsNullOrWhiteSpace(entry.SessionId) && Contains(sessionIds, entry.SessionId)) entry.SessionId = null;
        }

        private static bool Contains(IReadOnlyCollection<string>? set, string value)
        {
            if (set == null) return false;
            foreach (string candidate in set)
            {
                if (String.Equals(candidate, value, StringComparison.Ordinal)) return true;
            }

            return false;
        }
    }
}

namespace Xeno.Core.Helpers
{
    using System;

    /// <summary>
    /// PrettyId-backed identifier generator.
    /// </summary>
    public static class IdGenerator
    {
        /// <summary>
        /// Generated ID length, including prefix.
        /// </summary>
        public const int MaxIdLength = 32;

        private static readonly PrettyId.IdGenerator _Generator = new PrettyId.IdGenerator();

        /// <summary>
        /// Generate an ID with the supplied prefix.
        /// </summary>
        /// <param name="prefix">Identifier prefix.</param>
        /// <returns>K-sortable identifier.</returns>
        public static string NewId(string prefix)
        {
            if (prefix == null) throw new ArgumentNullException(nameof(prefix));

            string normalizedPrefix = NormalizePrefix(prefix);
            string id = _Generator.GenerateKSortable(normalizedPrefix, MaxIdLength);
            if (id.Length != MaxIdLength)
            {
                throw new InvalidOperationException("Generated PrettyId length mismatch.");
            }

            return id;
        }

        /// <summary>
        /// Generate a request history identifier.
        /// </summary>
        /// <returns>Request history identifier.</returns>
        public static string NewRequestHistoryId()
        {
            return NewId("req_");
        }

        private static string NormalizePrefix(string prefix)
        {
            if (String.IsNullOrWhiteSpace(prefix)) return String.Empty;
            return prefix.EndsWith("_", StringComparison.Ordinal) ? prefix : prefix + "_";
        }
    }
}

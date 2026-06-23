namespace Xeno.Core.Helpers
{
    using PrettyId;

    /// <summary>
    /// PrettyId-backed identifier generator.
    /// </summary>
    public static class IdGenerator
    {
        /// <summary>
        /// Default ID length.
        /// </summary>
        public const int DefaultIdLength = 48;

        private static readonly PrettyId.IdGenerator _Generator = new PrettyId.IdGenerator();

        /// <summary>
        /// Generate an ID with the supplied prefix.
        /// </summary>
        /// <param name="prefix">Identifier prefix.</param>
        /// <returns>K-sortable identifier.</returns>
        public static string NewId(string prefix)
        {
            return _Generator.GenerateKSortable(prefix, DefaultIdLength);
        }

        /// <summary>
        /// Generate a request history identifier.
        /// </summary>
        /// <returns>Request history identifier.</returns>
        public static string NewRequestHistoryId()
        {
            return NewId("req_");
        }
    }
}

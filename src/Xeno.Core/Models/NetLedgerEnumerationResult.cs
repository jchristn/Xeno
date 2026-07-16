namespace Xeno.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// NetLedger enumeration result.
    /// </summary>
    /// <typeparam name="T">Object type.</typeparam>
    public class NetLedgerEnumerationResult<T>
    {
        /// <summary>
        /// Result objects.
        /// </summary>
        public List<T> Objects { get; set; } = new List<T>();

        /// <summary>
        /// Alternate result item property.
        /// </summary>
        public List<T> Items { get; set; } = new List<T>();

        /// <summary>
        /// Total records.
        /// </summary>
        public long TotalRecords { get; set; } = 0;

        /// <summary>
        /// Continuation token.
        /// </summary>
        public string? ContinuationToken { get; set; } = null;

        /// <summary>
        /// Whether enumeration reached the end.
        /// </summary>
        public bool EndOfResults { get; set; } = true;
    }
}

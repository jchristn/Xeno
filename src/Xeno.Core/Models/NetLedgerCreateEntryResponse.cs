namespace Xeno.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// NetLedger create entry response.
    /// </summary>
    public class NetLedgerCreateEntryResponse
    {
        /// <summary>
        /// Created entry identifiers.
        /// </summary>
        public List<string> EntryIds { get; set; } = new List<string>();
    }
}

namespace Xeno.Core.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// NetLedger create account request.
    /// </summary>
    public class NetLedgerCreateAccountRequest
    {
        /// <summary>
        /// Account name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Initial balance.
        /// </summary>
        public decimal InitialBalance { get; set; } = 0m;

        /// <summary>
        /// Account labels.
        /// </summary>
        public List<string> Labels { get; set; } = new List<string>();

        /// <summary>
        /// Account tags.
        /// </summary>
        public Dictionary<string, string> Tags { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}

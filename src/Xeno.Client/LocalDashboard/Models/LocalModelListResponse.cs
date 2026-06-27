namespace Xeno.Client.LocalDashboard.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Local model list response.
    /// </summary>
    public class LocalModelListResponse
    {
        /// <summary>
        /// Models.
        /// </summary>
        public List<LocalModelEntry> Models { get; set; } = new List<LocalModelEntry>();
    }
}

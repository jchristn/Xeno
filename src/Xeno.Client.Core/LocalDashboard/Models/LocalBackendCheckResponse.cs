namespace Xeno.Client.LocalDashboard.Models
{
    using System.Collections.Generic;
    using Xeno.Client.Routing;

    /// <summary>
    /// Result of checking every routable local provider now.
    /// </summary>
    public class LocalBackendCheckResponse
    {
        /// <summary>
        /// Provider statuses. Never null.
        /// </summary>
        public List<LocalBackendStatus> Backends { get; set; } = new List<LocalBackendStatus>();
    }
}

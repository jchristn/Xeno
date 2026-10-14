namespace Xeno.Client.LocalDashboard.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Local provider list response.
    /// </summary>
    public class LocalProviderListResponse
    {
        /// <summary>
        /// Providers.
        /// </summary>
        public List<ClientRunnerSettings> Providers { get; set; } = new List<ClientRunnerSettings>();
    }
}

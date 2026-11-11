namespace Xeno.Client.Tools
{
    using System.Collections.Generic;

    /// <summary>
    /// Result of checking local providers.
    /// </summary>
    public sealed class XenoToolBackendList
    {
        /// <summary>
        /// Items.
        /// </summary>
        public List<Xeno.Client.Routing.LocalBackendStatus> Providers { get; set; } = new List<Xeno.Client.Routing.LocalBackendStatus>();
    }
}

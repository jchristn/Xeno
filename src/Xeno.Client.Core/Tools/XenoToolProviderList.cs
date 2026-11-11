namespace Xeno.Client.Tools
{
    using System.Collections.Generic;

    /// <summary>
    /// Providers configured on this machine.
    /// </summary>
    public sealed class XenoToolProviderList
    {
        /// <summary>
        /// Items.
        /// </summary>
        public List<XenoToolProvider> Providers { get; set; } = new List<XenoToolProvider>();
    }
}

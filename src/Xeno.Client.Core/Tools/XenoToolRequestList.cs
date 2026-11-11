namespace Xeno.Client.Tools
{
    using System.Collections.Generic;

    /// <summary>
    /// Recent local endpoint requests.
    /// </summary>
    public sealed class XenoToolRequestList
    {
        /// <summary>
        /// Items.
        /// </summary>
        public List<Xeno.Client.Routing.LocalInferenceRequestRecord> Requests { get; set; } = new List<Xeno.Client.Routing.LocalInferenceRequestRecord>();
    }
}

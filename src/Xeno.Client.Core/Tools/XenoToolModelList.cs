namespace Xeno.Client.Tools
{
    using System.Collections.Generic;

    /// <summary>
    /// Models available to chat with.
    /// </summary>
    public sealed class XenoToolModelList
    {
        /// <summary>
        /// Items.
        /// </summary>
        public List<XenoToolModel> Models { get; set; } = new List<XenoToolModel>();

        /// <summary>
        /// Why network models are missing, or null.
        /// </summary>
        public string? NetworkMessage { get; set; } = null;
    }
}

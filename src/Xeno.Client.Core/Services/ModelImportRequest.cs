namespace Xeno.Client.Services
{
    using System.Collections.Generic;

    /// <summary>
    /// Advertise several provider models at once.
    /// </summary>
    public sealed class ModelImportRequest
    {
        /// <summary>
        /// Models to advertise.
        /// </summary>
        public List<ModelImportItem> Models { get; set; } = new List<ModelImportItem>();
    }
}

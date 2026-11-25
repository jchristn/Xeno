namespace Xeno.Client.Services
{
    using System.Collections.Generic;

    /// <summary>
    /// Outcome of a model import.
    /// </summary>
    public sealed class ModelImportResult
    {
        /// <summary>
        /// Models newly advertised, as "provider/model".
        /// </summary>
        public List<string> Added { get; set; } = new List<string>();

        /// <summary>
        /// Models skipped because they were already advertised, as "provider/model".
        /// </summary>
        public List<string> Skipped { get; set; } = new List<string>();

        /// <summary>
        /// Models that could not be imported, with the reason.
        /// </summary>
        public List<string> Failed { get; set; } = new List<string>();
    }
}

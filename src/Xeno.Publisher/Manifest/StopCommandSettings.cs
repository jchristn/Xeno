namespace Xeno.Publisher.Manifest
{
    using System.Collections.Generic;

    /// <summary>
    /// A command installers run to stop the product before replacing or removing its files.
    /// </summary>
    public sealed class StopCommandSettings
    {
        /// <summary>
        /// Executable (without extension) to run.
        /// </summary>
        public string Executable { get; set; } = "";

        /// <summary>
        /// Arguments, for example ["stop"].
        /// </summary>
        public List<string> Arguments { get; set; } = new List<string>();
    }
}

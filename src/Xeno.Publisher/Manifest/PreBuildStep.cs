namespace Xeno.Publisher.Manifest
{
    using System.Collections.Generic;

    /// <summary>
    /// A command that runs once before the first publish in a Publisher run.
    /// </summary>
    public sealed class PreBuildStep
    {
        /// <summary>
        /// What the step does, for logs.
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// Working directory relative to the repository root.
        /// </summary>
        public string WorkingDirectory { get; set; } = "";

        /// <summary>
        /// Command to run, for example "npm". On Windows it runs through cmd.exe so .cmd shims resolve.
        /// </summary>
        public string Command { get; set; } = "";

        /// <summary>
        /// Arguments.
        /// </summary>
        public List<string> Arguments { get; set; } = new List<string>();

        /// <summary>
        /// Path relative to the repository root. When it exists the step is skipped, unless the Publisher runs with
        /// --clean. Empty to always run.
        /// </summary>
        public string SkipIfExists { get; set; } = "";
    }
}

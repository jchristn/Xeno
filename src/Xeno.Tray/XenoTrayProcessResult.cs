namespace Xeno.Tray
{
    /// <summary>
    /// Result from an invoked xeno CLI process.
    /// </summary>
    public sealed class XenoTrayProcessResult
    {
        /// <summary>
        /// Process exit code.
        /// </summary>
        public int ExitCode { get; set; } = 0;

        /// <summary>
        /// Captured standard output.
        /// </summary>
        public string StandardOutput { get; set; } = string.Empty;

        /// <summary>
        /// Captured standard error.
        /// </summary>
        public string StandardError { get; set; } = string.Empty;

        /// <summary>
        /// Whether the command succeeded.
        /// </summary>
        public bool Succeeded
        {
            get
            {
                return ExitCode == 0;
            }
        }
    }
}

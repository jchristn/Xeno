namespace Test.Shared
{
    /// <summary>
    /// Result from invoking the Xeno client CLI during tests.
    /// </summary>
    internal sealed class ClientCommandResult
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
    }
}

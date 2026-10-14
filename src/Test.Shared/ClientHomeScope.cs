namespace Test.Shared
{
    using System;
    using System.IO;

    /// <summary>
    /// Points XENO_CLIENT_HOME at a fresh temporary directory for the lifetime of the scope, then restores the
    /// previous value and deletes the directory.
    /// </summary>
    public sealed class ClientHomeScope : IDisposable
    {
        private readonly string? _Previous;

        /// <summary>
        /// Temporary client home directory.
        /// </summary>
        public string Directory { get; }

        /// <summary>
        /// Create the scope.
        /// </summary>
        public ClientHomeScope()
        {
            _Previous = Environment.GetEnvironmentVariable("XENO_CLIENT_HOME");
            Directory = Path.Combine(Path.GetTempPath(), "xeno-frontend-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(Directory);
            Environment.SetEnvironmentVariable("XENO_CLIENT_HOME", Directory);
        }

        /// <summary>
        /// Restore the previous value and delete the directory.
        /// </summary>
        public void Dispose()
        {
            Environment.SetEnvironmentVariable("XENO_CLIENT_HOME", _Previous);
            try
            {
                if (System.IO.Directory.Exists(Directory)) System.IO.Directory.Delete(Directory, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}

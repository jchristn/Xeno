namespace Xeno.Client.Services
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading;

    /// <summary>
    /// Reads and writes the small JSON files under the client home (client.json, client-state.json) so that the worker,
    /// the CLI, and the desktop, terminal, and tray apps can use them at the same time. Reads share the file for reading,
    /// writing, and deletion, so a writer can replace it while it is open. Writes go to a temporary file that then replaces
    /// the target, so a reader never sees a half-written file. Both retry briefly on the sharing violations Windows reports
    /// when another process has the file open. Thread safe.
    /// </summary>
    public static class ClientFileIo
    {
        private const int _Attempts = 10;
        private const int _RetryDelayMs = 25;

        /// <summary>
        /// Read a whole text file.
        /// </summary>
        /// <param name="path">File path.</param>
        /// <returns>File contents.</returns>
        /// <exception cref="ArgumentException">path is null or empty.</exception>
        /// <exception cref="FileNotFoundException">The file does not exist.</exception>
        /// <exception cref="IOException">The file stayed locked through every retry.</exception>
        public static string ReadAllText(string path)
        {
            if (String.IsNullOrEmpty(path)) throw new ArgumentException("Path is required.", nameof(path));
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        return reader.ReadToEnd();
                    }
                }
                catch (FileNotFoundException)
                {
                    throw;
                }
                catch (DirectoryNotFoundException)
                {
                    throw;
                }
                catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) && attempt < _Attempts)
                {
                    Thread.Sleep(_RetryDelayMs * attempt);
                }
            }
        }

        /// <summary>
        /// Replace a text file atomically, creating its directory when needed.
        /// </summary>
        /// <param name="path">File path.</param>
        /// <param name="contents">Contents. Null writes an empty file.</param>
        /// <exception cref="ArgumentException">path is null or empty.</exception>
        /// <exception cref="IOException">The file stayed locked through every retry.</exception>
        public static void WriteAllTextAtomic(string path, string? contents)
        {
            if (String.IsNullOrEmpty(path)) throw new ArgumentException("Path is required.", nameof(path));
            string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!String.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            string tempFile = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(tempFile, contents ?? String.Empty, new UTF8Encoding(false));
            try
            {
                for (int attempt = 1; ; attempt++)
                {
                    try
                    {
                        File.Move(tempFile, path, true);
                        return;
                    }
                    catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) && attempt < _Attempts)
                    {
                        Thread.Sleep(_RetryDelayMs * attempt);
                    }
                }
            }
            finally
            {
                try
                {
                    if (File.Exists(tempFile)) File.Delete(tempFile);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                }
            }
        }
    }
}

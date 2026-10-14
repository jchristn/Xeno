namespace Xeno.Client.Hosting
{
    using System;
    using System.IO;

    /// <summary>
    /// Exclusive lock file that keeps one instance of a front end running per client home directory.
    /// The lock is released when the instance is disposed or the process exits.
    /// </summary>
    public sealed class SingleInstanceLock : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Lock file path.
        /// </summary>
        public string FilePath { get; }

        #endregion

        #region Private-Members

        private FileStream? _Stream;

        #endregion

        #region Constructors-and-Factories

        private SingleInstanceLock(string filePath, FileStream stream)
        {
            FilePath = filePath;
            _Stream = stream;
        }

        /// <summary>
        /// Try to acquire a named lock in the client home directory.
        /// </summary>
        /// <param name="name">Lock name, for example "desktop" or "tray".</param>
        /// <returns>Lock, or null when another instance holds it.</returns>
        /// <exception cref="ArgumentException">Name is empty.</exception>
        public static SingleInstanceLock? TryAcquire(string name)
        {
            if (String.IsNullOrWhiteSpace(name)) throw new ArgumentException("A lock name is required.", nameof(name));
            return TryAcquire(ClientSettings.SettingsDirectory, name);
        }

        /// <summary>
        /// Try to acquire a named lock in a directory.
        /// </summary>
        /// <param name="directory">Directory for the lock file. Created when missing.</param>
        /// <param name="name">Lock name.</param>
        /// <returns>Lock, or null when another instance holds it.</returns>
        /// <exception cref="ArgumentException">Directory or name is empty.</exception>
        public static SingleInstanceLock? TryAcquire(string directory, string name)
        {
            if (String.IsNullOrWhiteSpace(directory)) throw new ArgumentException("A lock directory is required.", nameof(directory));
            if (String.IsNullOrWhiteSpace(name)) throw new ArgumentException("A lock name is required.", nameof(name));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, name + ".lock");
            try
            {
                FileStream stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                return new SingleInstanceLock(path, stream);
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Release the lock.
        /// </summary>
        public void Dispose()
        {
            FileStream? stream = _Stream;
            _Stream = null;
            stream?.Dispose();
        }

        #endregion
    }
}

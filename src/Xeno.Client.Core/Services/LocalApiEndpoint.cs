namespace Xeno.Client.Services
{
    using System;
    using System.IO;
    using System.Text.Json;
    using Xeno.Core.Serialization;

    /// <summary>
    /// Where the running client's local API listens and the token it requires. The client writes it to
    /// ~/.xeno/local-api.json (readable only by the user on Linux and macOS) while the local dashboard runs, so the
    /// CLI, desktop app, and terminal app can call the chat API. Removed when the client stops.
    /// </summary>
    public sealed class LocalApiEndpoint
    {
        #region Public-Members

        /// <summary>
        /// Base URL, for example "http://127.0.0.1:9343".
        /// </summary>
        public string Url { get; set; } = String.Empty;

        /// <summary>
        /// Value for the X-Xeno-Local-Token header.
        /// </summary>
        public string Token { get; set; } = String.Empty;

        /// <summary>
        /// Client process id.
        /// </summary>
        public int ProcessId { get; set; } = 0;

        /// <summary>
        /// Written, UTC.
        /// </summary>
        public DateTime StartedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// File path.
        /// </summary>
        public static string FilePath
        {
            get { return Path.Combine(ClientSettings.SettingsDirectory, "local-api.json"); }
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Write the endpoint file.
        /// </summary>
        /// <param name="endpoint">Endpoint.</param>
        /// <exception cref="ArgumentNullException">endpoint is null.</exception>
        /// <exception cref="IOException">The file could not be written.</exception>
        public static void Publish(LocalApiEndpoint endpoint)
        {
            ArgumentNullException.ThrowIfNull(endpoint);
            string path = FilePath;
            ClientFileIo.WriteAllTextAtomic(path, JsonUtil.Serialize(endpoint, true));
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
        }

        /// <summary>
        /// Read the endpoint file.
        /// </summary>
        /// <returns>Endpoint, or null when the client's local API is not running.</returns>
        public static LocalApiEndpoint? Read()
        {
            try
            {
                string path = FilePath;
                if (!File.Exists(path)) return null;
                LocalApiEndpoint? endpoint = JsonUtil.Deserialize<LocalApiEndpoint>(ClientFileIo.ReadAllText(path));
                if (endpoint == null || String.IsNullOrWhiteSpace(endpoint.Url)) return null;
                return endpoint;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException || ex is InvalidDataException)
            {
                return null;
            }
        }

        /// <summary>
        /// Remove the endpoint file when it belongs to this process.
        /// </summary>
        /// <param name="processId">Process id that wrote it.</param>
        public static void Withdraw(int processId)
        {
            try
            {
                LocalApiEndpoint? current = Read();
                if (current != null && current.ProcessId != processId) return;
                if (File.Exists(FilePath)) File.Delete(FilePath);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
            }
        }

        #endregion
    }
}

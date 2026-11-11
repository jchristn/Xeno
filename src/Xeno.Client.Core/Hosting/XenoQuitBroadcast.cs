namespace Xeno.Client.Hosting
{
    using System;
    using System.IO;
    using Xeno.Client.Services;
    using Xeno.Core.Serialization;

    /// <summary>
    /// Tells every running Xeno app on this client home to quit. The tray agent posts a request when it quits; the desktop
    /// app and the terminal app check for one made after they started, so an app opened later is not affected. Thread safe.
    /// </summary>
    public static class XenoQuitBroadcast
    {
        #region Public-Members

        /// <summary>
        /// Request file in the client home.
        /// </summary>
        public static string FilePath
        {
            get { return Path.Combine(ClientSettings.SettingsDirectory, "quit-request.json"); }
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Ask every running Xeno app to quit.
        /// </summary>
        public static void Post()
        {
            Directory.CreateDirectory(ClientSettings.SettingsDirectory);
            ClientFileIo.WriteAllTextAtomic(FilePath, JsonUtil.Serialize(new XenoQuitRequest { RequestedUtc = DateTime.UtcNow, ProcessId = Environment.ProcessId }, true));
        }

        /// <summary>
        /// True when a quit was requested after the given time, by another process.
        /// </summary>
        /// <param name="startedUtc">When the asking app started.</param>
        /// <returns>True to quit.</returns>
        public static bool IsRequestedSince(DateTime startedUtc)
        {
            try
            {
                if (!File.Exists(FilePath)) return false;
                XenoQuitRequest? request = JsonUtil.Deserialize<XenoQuitRequest>(ClientFileIo.ReadAllText(FilePath));
                return request != null && request.ProcessId != Environment.ProcessId && request.RequestedUtc > startedUtc.ToUniversalTime();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is InvalidDataException || ex is System.Text.Json.JsonException)
            {
                return false;
            }
        }

        #endregion
    }
}

namespace Xeno.Client.Routing
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using Xeno.Core.Serialization;

    /// <summary>
    /// What the running local inference listener knows that other processes cannot work out from settings: provider
    /// health, concurrency counters, and request counters. The worker writes it; the CLI, desktop app, terminal app,
    /// tray, and local dashboard read it.
    /// </summary>
    public class LocalInferenceRuntimeStatus
    {
        /// <summary>
        /// Status file path.
        /// </summary>
        public static string FilePath
        {
            get
            {
                return Path.Combine(ClientSettings.SettingsDirectory, "local-inference-status.json");
            }
        }

        /// <summary>
        /// When the listener wrote this status (UTC).
        /// </summary>
        public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When the listener started (UTC).
        /// </summary>
        public DateTime StartedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Provider health snapshots. Never null.
        /// </summary>
        public List<LocalBackendStatus> Backends { get; set; } = new List<LocalBackendStatus>();

        /// <summary>
        /// Provider concurrency counters. Never null.
        /// </summary>
        public List<ProviderConcurrencySnapshot> Concurrency { get; set; } = new List<ProviderConcurrencySnapshot>();

        /// <summary>
        /// Requests answered by a provider on this machine since the listener started.
        /// </summary>
        public long LocalRequests { get; set; } = 0;

        /// <summary>
        /// Requests sent to the Xeno network since the listener started, including fallbacks.
        /// </summary>
        public long NetworkRequests { get; set; } = 0;

        /// <summary>
        /// Requests that fell back to the network since the listener started.
        /// </summary>
        public long Fallbacks { get; set; } = 0;

        /// <summary>
        /// Requests that ended with a status of 400 or higher since the listener started.
        /// </summary>
        public long Errors { get; set; } = 0;

        /// <summary>
        /// Read the status file.
        /// </summary>
        /// <returns>Status, or null when the listener has never written one or the file cannot be read.</returns>
        public static LocalInferenceRuntimeStatus? Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                using (FileStream stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (StreamReader reader = new StreamReader(stream))
                {
                    return JsonUtil.Deserialize<LocalInferenceRuntimeStatus>(reader.ReadToEnd());
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException || ex is System.IO.InvalidDataException)
            {
                return null;
            }
        }

        /// <summary>
        /// Write the status file atomically (write a temporary file, then move it into place).
        /// </summary>
        public void Save()
        {
            try
            {
                Directory.CreateDirectory(ClientSettings.SettingsDirectory);
                string temporary = FilePath + ".tmp";
                File.WriteAllText(temporary, JsonUtil.Serialize(this, true));
                File.Move(temporary, FilePath, true);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
            }
        }

        /// <summary>
        /// Delete the status file, for example when the listener stops.
        /// </summary>
        public static void Delete()
        {
            try
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
            }
        }
    }
}

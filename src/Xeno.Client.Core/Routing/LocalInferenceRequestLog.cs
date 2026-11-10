namespace Xeno.Client.Routing
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Serialization;

    /// <summary>
    /// Metadata-only log of local inference listener requests, kept as JSON lines under the client home so the CLI,
    /// the desktop app, and the terminal app can read what the worker wrote. Bodies are never stored, whatever the request
    /// history settings say, because no diagnostic setting permits prompt capture for the listener.
    /// Thread safe within a process; appends from one writer process.
    /// </summary>
    public static class LocalInferenceRequestLog
    {
        private static readonly object _Lock = new object();
        private static int _AppendsSinceTrim;
        private static string _CounterPath = String.Empty;

        /// <summary>
        /// Log file path.
        /// </summary>
        public static string FilePath
        {
            get
            {
                return Path.Combine(ClientSettings.SettingsDirectory, "local-inference-requests.jsonl");
            }
        }

        /// <summary>
        /// Append a record and trim the file to <paramref name="maxEntries"/> now and then.
        /// </summary>
        /// <param name="record">Record. Null is ignored.</param>
        /// <param name="maxEntries">Records to keep. Minimum 10. Default 1000.</param>
        public static void Append(LocalInferenceRequestRecord? record, int maxEntries = 1000)
        {
            if (record == null) return;
            string line = JsonUtil.Serialize(record) + "\n";
            lock (_Lock)
            {
                try
                {
                    Directory.CreateDirectory(ClientSettings.SettingsDirectory);
                    string path = FilePath;
                    if (!String.Equals(path, _CounterPath, StringComparison.Ordinal))
                    {
                        _CounterPath = path;
                        _AppendsSinceTrim = 0;
                    }

                    File.AppendAllText(path, line, Encoding.UTF8);
                    _AppendsSinceTrim++;
                    if (_AppendsSinceTrim >= 50) Trim(Math.Max(10, maxEntries));
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                }
            }
        }

        /// <summary>
        /// Read records newest first.
        /// </summary>
        /// <param name="skip">Records to skip. Minimum 0.</param>
        /// <param name="take">Records to return. Clamped to 1 through 1000.</param>
        /// <returns>Records, never null.</returns>
        public static List<LocalInferenceRequestRecord> Read(int skip = 0, int take = 100)
        {
            return ReadAll().Skip(Math.Max(0, skip)).Take(Math.Min(1000, Math.Max(1, take))).ToList();
        }

        /// <summary>
        /// Read records newest first.
        /// </summary>
        /// <param name="skip">Records to skip. Minimum 0.</param>
        /// <param name="take">Records to return. Clamped to 1 through 1000.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Records, never null.</returns>
        public static Task<List<LocalInferenceRequestRecord>> ReadAsync(int skip, int take, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult(Read(skip, take));
        }

        /// <summary>
        /// Count stored records.
        /// </summary>
        /// <returns>Count.</returns>
        public static int Count()
        {
            return ReadLines().Count;
        }

        /// <summary>
        /// Delete every record.
        /// </summary>
        public static void Clear()
        {
            lock (_Lock)
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

        private static List<LocalInferenceRequestRecord> ReadAll()
        {
            List<LocalInferenceRequestRecord> records = new List<LocalInferenceRequestRecord>();
            List<string> lines = ReadLines();
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                try
                {
                    LocalInferenceRequestRecord? record = JsonUtil.Deserialize<LocalInferenceRequestRecord>(lines[i]);
                    if (record != null) records.Add(record);
                }
                catch (JsonException)
                {
                }
            }

            return records;
        }

        private static List<string> ReadLines()
        {
            lock (_Lock)
            {
                try
                {
                    if (!File.Exists(FilePath)) return new List<string>();
                    using (FileStream stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
                    }
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    return new List<string>();
                }
            }
        }

        private static void Trim(int maxEntries)
        {
            _AppendsSinceTrim = 0;
            List<string> lines = File.ReadAllText(FilePath, Encoding.UTF8).Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
            if (lines.Count <= maxEntries) return;
            File.WriteAllText(FilePath, String.Join("\n", lines.Skip(lines.Count - maxEntries)) + "\n", Encoding.UTF8);
        }
    }
}

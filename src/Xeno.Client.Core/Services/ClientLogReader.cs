namespace Xeno.Client.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Reads and follows the client worker log files. Shared by the CLI, the local dashboard, the desktop app, and the
    /// terminal app. Files are opened with shared read/write/delete access so the worker can keep writing. Thread-safe.
    /// </summary>
    public static class ClientLogReader
    {
        #region Public-Members

        /// <summary>
        /// Severity filters accepted by <see cref="ReadTail(string, int, string?)"/>. "all" disables filtering.
        /// </summary>
        public static readonly IReadOnlyList<string> SeverityFilters = new List<string> { "all", "info", "warn", "error", "debug" };

        /// <summary>
        /// Poll interval used while following a log, in milliseconds.
        /// </summary>
        public const int FollowPollIntervalMs = 250;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Read the last lines of a log file.
        /// </summary>
        /// <param name="filename">Log file.</param>
        /// <param name="tail">Maximum number of lines to return. Zero or less returns no lines.</param>
        /// <param name="severity">Severity filter (all, info, warn, error, debug). Null means all.</param>
        /// <returns>Matching lines, oldest first. An empty list when the file does not exist.</returns>
        public static List<string> ReadTail(string filename, int tail, string? severity)
        {
            List<string> lines = new List<string>();
            if (String.IsNullOrWhiteSpace(filename) || !File.Exists(filename) || tail <= 0) return lines;
            using (FileStream stream = OpenShared(filename))
            using (StreamReader reader = new StreamReader(stream))
            {
                while (true)
                {
                    string? line = reader.ReadLine();
                    if (line == null) break;
                    if (!Matches(line, severity)) continue;
                    lines.Add(line);
                    if (lines.Count > tail) lines.RemoveAt(0);
                }
            }

            return lines;
        }

        /// <summary>
        /// Read the last lines of a log file asynchronously.
        /// </summary>
        /// <param name="filename">Log file.</param>
        /// <param name="tail">Maximum number of lines to return. Zero or less returns no lines.</param>
        /// <param name="severity">Severity filter (all, info, warn, error, debug). Null means all.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Matching lines, oldest first. An empty list when the file does not exist.</returns>
        public static Task<List<string>> ReadTailAsync(string filename, int tail, string? severity, CancellationToken token)
        {
            return ReadTailAsync(filename, tail, severity, null, token);
        }

        /// <summary>
        /// Read the last lines of a log file written at or after a time.
        /// </summary>
        /// <param name="filename">Log file.</param>
        /// <param name="tail">Maximum lines.</param>
        /// <param name="severity">Severity filter (all, info, warn, error, debug). Null means all.</param>
        /// <param name="sinceUtc">Earliest time to include, or null for any time. A line without a timestamp (such as a
        /// stack trace line) goes with the line before it.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Matching lines, oldest first. An empty list when the file does not exist.</returns>
        public static async Task<List<string>> ReadTailAsync(string filename, int tail, string? severity, DateTime? sinceUtc, CancellationToken token)
        {
            List<string> lines = new List<string>();
            if (String.IsNullOrWhiteSpace(filename) || !File.Exists(filename) || tail <= 0) return lines;
            bool inWindow = sinceUtc == null;
            using (FileStream stream = OpenShared(filename))
            using (StreamReader reader = new StreamReader(stream))
            {
                while (!token.IsCancellationRequested)
                {
                    string? line = await reader.ReadLineAsync(token).ConfigureAwait(false);
                    if (line == null) break;
                    if (sinceUtc != null)
                    {
                        DateTime? stamp = Timestamp(line);
                        if (stamp != null) inWindow = stamp.Value >= sinceUtc.Value.ToUniversalTime();
                        if (!inWindow) continue;
                    }

                    if (!Matches(line, severity)) continue;
                    lines.Add(line);
                    if (lines.Count > tail) lines.RemoveAt(0);
                }
            }

            return lines;
        }

        /// <summary>
        /// The UTC time at the start of a log line ("2026-11-11 16:55:01.690684Z ..."), or null when it has none.
        /// </summary>
        /// <param name="line">Log line.</param>
        /// <returns>Time, or null.</returns>
        public static DateTime? Timestamp(string? line)
        {
            if (String.IsNullOrEmpty(line) || line.Length < 20 || !Char.IsDigit(line[0])) return null;
            int end = line.IndexOf('Z');
            if (end < 19 || end > 32) return null;
            return DateTime.TryParse(line.Substring(0, end + 1), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out DateTime parsed) ? parsed : null;
        }

        /// <summary>
        /// Follow a log file, invoking a callback for each new line until cancelled.
        /// </summary>
        /// <param name="filename">Log file. Must exist.</param>
        /// <param name="onLine">Callback invoked for each new line, on a background thread.</param>
        /// <param name="token">Cancellation token. Cancellation ends the follow without throwing.</param>
        /// <returns>Task that completes when the token is cancelled.</returns>
        /// <exception cref="ArgumentNullException">Callback is null.</exception>
        /// <exception cref="FileNotFoundException">The log file does not exist.</exception>
        public static async Task FollowAsync(string filename, Action<string> onLine, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(onLine);
            if (!File.Exists(filename)) throw new FileNotFoundException("Log file not found.", filename);
            try
            {
                using (FileStream stream = OpenShared(filename))
                using (StreamReader reader = new StreamReader(stream))
                {
                    stream.Seek(0, SeekOrigin.End);
                    while (!token.IsCancellationRequested)
                    {
                        string? line = await reader.ReadLineAsync(token).ConfigureAwait(false);
                        if (line != null)
                        {
                            onLine(line);
                            continue;
                        }

                        await Task.Delay(FollowPollIntervalMs, token).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
        }

        /// <summary>
        /// Determine whether a log line matches a severity filter.
        /// </summary>
        /// <param name="line">Log line.</param>
        /// <param name="severity">Severity filter. Null, empty, or "all" matches every line.</param>
        /// <returns>True when the line matches.</returns>
        public static bool Matches(string line, string? severity)
        {
            if (String.IsNullOrWhiteSpace(severity) || String.Equals(severity, "all", StringComparison.OrdinalIgnoreCase)) return true;
            if (String.IsNullOrEmpty(line)) return false;
            return line.IndexOf("[" + severity.ToUpperInvariant() + "]", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        #endregion

        #region Private-Methods

        private static FileStream OpenShared(string filename)
        {
            return new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        }

        #endregion
    }
}

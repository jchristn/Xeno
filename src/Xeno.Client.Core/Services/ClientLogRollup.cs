namespace Xeno.Client.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    /// <summary>
    /// Collapses routine server messages in the worker log so they do not bury everything else. Each run of rolled-up
    /// messages, uninterrupted by any other line, becomes one line per message type that keeps the latest line's
    /// timestamp and severity and adds a count, for example
    /// "Server message: server.telemetry.ack (5 since 11:16:39)". Used by the desktop and terminal log views. Stateless
    /// and thread-safe.
    /// </summary>
    public static class ClientLogRollup
    {
        #region Public-Members

        /// <summary>
        /// Server messages that are rolled up.
        /// </summary>
        public static readonly IReadOnlyList<string> RolledUpMessages = new List<string> { "server.telemetry.request", "server.telemetry.ack" };

        /// <summary>
        /// Text that precedes the message type in a server message log line.
        /// </summary>
        public const string ServerMessagePrefix = "Server message: ";

        #endregion

        #region Public-Methods

        /// <summary>
        /// Collapse runs of rolled-up server messages.
        /// </summary>
        /// <param name="lines">Log lines, oldest first.</param>
        /// <returns>Lines with each run collapsed, oldest first.</returns>
        /// <exception cref="ArgumentNullException">Lines is null.</exception>
        public static List<string> Collapse(IEnumerable<string> lines)
        {
            ArgumentNullException.ThrowIfNull(lines);
            List<string> result = new List<string>();
            List<ClientLogRollupRun> run = new List<ClientLogRollupRun>();
            int position = 0;
            foreach (string line in lines)
            {
                string? message = RolledUpMessage(line);
                if (message == null)
                {
                    Flush(run, result);
                    result.Add(line);
                    continue;
                }

                ClientLogRollupRun? existing = run.FirstOrDefault(item => String.Equals(item.Message, message, StringComparison.Ordinal));
                if (existing == null)
                {
                    existing = new ClientLogRollupRun(message, line);
                    run.Add(existing);
                }

                existing.Add(line, position++);
            }

            Flush(run, result);
            return result;
        }

        /// <summary>
        /// The rolled-up server message a log line reports, or null when the line is not one.
        /// </summary>
        /// <param name="line">Log line.</param>
        /// <returns>Message type, or null.</returns>
        public static string? RolledUpMessage(string? line)
        {
            if (String.IsNullOrEmpty(line)) return null;
            int index = line.IndexOf(ServerMessagePrefix, StringComparison.Ordinal);
            if (index < 0) return null;
            string message = line.Substring(index + ServerMessagePrefix.Length).Trim().TrimEnd('.');
            return RolledUpMessages.FirstOrDefault(item => String.Equals(item, message, StringComparison.Ordinal));
        }

        /// <summary>
        /// Local time of day (HH:mm:ss) of a log line's UTC timestamp, or null when the line has none.
        /// </summary>
        /// <param name="line">Log line, starting with a timestamp such as "2026-10-14 18:16:39.429377Z".</param>
        /// <returns>Local time of day, or null.</returns>
        public static string? LocalTimeOfDay(string? line)
        {
            if (String.IsNullOrEmpty(line)) return null;
            string[] parts = line.Split(' ', 3);
            if (parts.Length < 2) return null;
            if (!DateTime.TryParse(parts[0] + " " + parts[1], CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime utc)) return null;
            return DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        }

        #endregion

        #region Private-Methods

        private static void Flush(List<ClientLogRollupRun> run, List<string> result)
        {
            foreach (ClientLogRollupRun item in run.OrderBy(entry => entry.LastPosition))
            {
                result.Add(item.Describe());
            }

            run.Clear();
        }

        #endregion
    }
}

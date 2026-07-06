namespace Xeno.Tray
{
    using System.Globalization;
    using System.Text;

    /// <summary>
    /// Formats tray status, menu, and statistics text.
    /// </summary>
    public static class XenoTrayStatusFormatter
    {
        /// <summary>
        /// Build menu state for a status snapshot.
        /// </summary>
        /// <param name="status">Status snapshot.</param>
        /// <returns>Menu state.</returns>
        public static XenoTrayMenuState BuildMenuState(XenoTrayStatus status)
        {
            string state = StatusText(status);
            return new XenoTrayMenuState
            {
                StatusHeader = "Status: " + state,
                ToolTip = "Xeno Client - " + state,
                StartEnabled = !status.Running,
                OpenDashboardEnabled = status.LocalDashboardListening && !string.IsNullOrWhiteSpace(status.LocalDashboardUrl),
                ExitClientEnabled = status.Running
            };
        }

        /// <summary>
        /// Format statistics for display.
        /// </summary>
        /// <param name="status">Status snapshot.</param>
        /// <param name="statistics">Statistics snapshot.</param>
        /// <returns>Display text.</returns>
        public static string BuildStatisticsText(XenoTrayStatus status, XenoTrayStatistics statistics)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Xeno Client");
            builder.AppendLine("Status: " + StatusText(status));
            builder.AppendLine("Client: " + Empty(status.Name));
            builder.AppendLine("Client ID: " + Empty(status.ClientId));
            builder.AppendLine("WebSocket: " + Empty(status.WebSocketUrl));
            builder.AppendLine("Process ID: " + (status.ProcessId == null ? "-" : status.ProcessId.Value.ToString(CultureInfo.InvariantCulture)));
            builder.AppendLine();
            builder.AppendLine("Requests: " + statistics.Requests.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("Failures: " + statistics.Failures.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("Streaming: " + statistics.StreamingRequests.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("Average latency: " + (statistics.AverageLatencyMs == null ? "-" : statistics.AverageLatencyMs.Value.ToString("0.0", CultureInfo.InvariantCulture) + " ms"));
            builder.AppendLine("Oldest request UTC: " + FormatUtc(statistics.OldestRequestUtc));
            builder.AppendLine("Newest request UTC: " + FormatUtc(statistics.NewestRequestUtc));
            if (!string.IsNullOrWhiteSpace(statistics.Message))
            {
                builder.AppendLine();
                builder.AppendLine(statistics.Message);
            }

            return builder.ToString().TrimEnd();
        }

        /// <summary>
        /// Format a compact status string.
        /// </summary>
        /// <param name="status">Status snapshot.</param>
        /// <returns>Status text.</returns>
        public static string StatusText(XenoTrayStatus status)
        {
            if (status.Running && status.Connected && status.Registered) return "Connected";
            if (status.Running) return "Starting";
            return "Stopped";
        }

        private static string Empty(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value;
        }

        private static string FormatUtc(System.DateTime? value)
        {
            return value == null ? "-" : value.Value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);
        }
    }
}

namespace Xeno.Client.Presentation
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using Xeno.Client.LocalDashboard.Models;
    using Xeno.Client.Services;

    /// <summary>
    /// Formats a request history entry for detail views in the desktop app and the terminal app. Bodies are shown only
    /// when the caller asks for them; they are stored only when history body persistence is enabled.
    /// </summary>
    public static class RequestDetailFormatter
    {
        /// <summary>
        /// Summary fields.
        /// </summary>
        /// <param name="entry">Entry.</param>
        /// <returns>Label and value pairs.</returns>
        /// <exception cref="ArgumentNullException">Entry is null.</exception>
        public static List<KeyValuePair<string, string>> Summary(LocalRequestHistoryEntry entry)
        {
            ArgumentNullException.ThrowIfNull(entry);
            LocalRequestTelemetry telemetry = entry.Telemetry;
            return new List<KeyValuePair<string, string>>
            {
                Pair("Request ID", entry.Id),
                Pair("Received", DisplayFormat.Local(entry.ReceivedUtc)),
                Pair("Completed", DisplayFormat.Local(entry.CompletedUtc)),
                Pair("Request", entry.Method + " " + entry.Path),
                Pair("Model", DisplayFormat.OrDash(entry.ModelName)),
                Pair("Provider", DisplayFormat.OrDash(entry.ProviderName) + (String.IsNullOrWhiteSpace(entry.ApiType) ? String.Empty : " (" + entry.ApiType + ")")),
                Pair("Outcome", entry.Outcome + (entry.StatusCode == null ? String.Empty : " (HTTP " + entry.StatusCode.Value.ToString(CultureInfo.InvariantCulture) + ")")),
                Pair("Error", DisplayFormat.OrDash(entry.Error)),
                Pair("Streamed", DisplayFormat.YesNo(entry.Streamed) + (entry.Streamed ? " (" + telemetry.StreamChunkCount + " chunks, " + telemetry.StreamEventCount + " events)" : String.Empty)),
                Pair("Duration", DisplayFormat.Milliseconds(telemetry.DurationMs)),
                Pair("Time to headers", DisplayFormat.Milliseconds(telemetry.TimeToHeadersMs)),
                Pair("Time to first byte", DisplayFormat.Milliseconds(telemetry.TimeToFirstByteMs)),
                Pair("Tokens", Tokens(telemetry)),
                Pair("Requestor", DisplayFormat.OrDash(entry.RequestorPrincipalType) + " " + DisplayFormat.OrDash(entry.RequestorPrincipalId) + (String.IsNullOrWhiteSpace(entry.RequestorIp) ? String.Empty : " from " + entry.RequestorIp)),
                Pair("Provider health", DisplayFormat.OrDash(entry.ProviderTelemetryHealthStatus))
            };
        }

        /// <summary>
        /// Timeline lines, oldest first, with offsets from the receive time.
        /// </summary>
        /// <param name="entry">Entry.</param>
        /// <returns>Lines.</returns>
        /// <exception cref="ArgumentNullException">Entry is null.</exception>
        public static List<string> Timeline(LocalRequestHistoryEntry entry)
        {
            ArgumentNullException.ThrowIfNull(entry);
            return entry.Timeline
                .OrderBy(item => item.TimestampUtc)
                .Select(item => "+" + (item.TimestampUtc - entry.ReceivedUtc).TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture).PadLeft(6) + " ms  " + item.Stage + (String.IsNullOrWhiteSpace(item.Message) ? String.Empty : "  " + item.Message))
                .ToList();
        }

        /// <summary>
        /// Headers as "name: value" lines.
        /// </summary>
        /// <param name="headers">Headers.</param>
        /// <returns>Text, or "(none)".</returns>
        public static string Headers(Dictionary<string, string>? headers)
        {
            if (headers == null || headers.Count == 0) return "(none)";
            return String.Join(Environment.NewLine, headers.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase).Select(item => item.Key + ": " + item.Value));
        }

        /// <summary>
        /// Full plain-text detail, for copying.
        /// </summary>
        /// <param name="entry">Entry.</param>
        /// <param name="includeBodies">True to include request and response bodies.</param>
        /// <returns>Text.</returns>
        /// <exception cref="ArgumentNullException">Entry is null.</exception>
        public static string ToText(LocalRequestHistoryEntry entry, bool includeBodies)
        {
            ArgumentNullException.ThrowIfNull(entry);
            StringBuilder builder = new StringBuilder();
            foreach (KeyValuePair<string, string> pair in Summary(entry)) builder.AppendLine(pair.Key.PadRight(20) + pair.Value);
            builder.AppendLine();
            builder.AppendLine("Timeline");
            foreach (string line in Timeline(entry)) builder.AppendLine("  " + line);
            builder.AppendLine();
            builder.AppendLine("Request headers");
            builder.AppendLine(Headers(entry.RequestHeaders));
            builder.AppendLine();
            builder.AppendLine("Response headers");
            builder.AppendLine(Headers(entry.ResponseHeaders));
            if (includeBodies)
            {
                builder.AppendLine();
                builder.AppendLine("Request body");
                builder.AppendLine(String.IsNullOrEmpty(entry.RequestBody) ? "(not stored)" : entry.RequestBody);
                builder.AppendLine();
                builder.AppendLine("Response body");
                builder.AppendLine(String.IsNullOrEmpty(entry.ResponseBody) ? "(not stored)" : entry.ResponseBody);
            }

            return builder.ToString();
        }

        /// <summary>
        /// Full detail as Markdown, for the terminal app's viewer: the summary as a table, then the timeline, headers,
        /// and bodies in code blocks. Values are escaped and bodies are fenced, so they render verbatim.
        /// </summary>
        /// <param name="entry">Entry.</param>
        /// <param name="includeBodies">True to include request and response bodies.</param>
        /// <returns>Markdown.</returns>
        /// <exception cref="ArgumentNullException">Entry is null.</exception>
        public static string ToMarkdown(LocalRequestHistoryEntry entry, bool includeBodies)
        {
            ArgumentNullException.ThrowIfNull(entry);
            List<string?> blocks = new List<string?>
            {
                XenoMarkdown.Details(Summary(entry)),
                XenoMarkdown.Heading("Timeline"),
                XenoMarkdown.CodeBlock(entry.Timeline.Count == 0 ? "(none)" : String.Join("\n", Timeline(entry))),
                XenoMarkdown.Heading("Request headers"),
                XenoMarkdown.CodeBlock(Headers(entry.RequestHeaders)),
                XenoMarkdown.Heading("Response headers"),
                XenoMarkdown.CodeBlock(Headers(entry.ResponseHeaders))
            };

            if (includeBodies)
            {
                blocks.Add(XenoMarkdown.Heading("Request body"));
                blocks.Add(Body(entry.RequestBody));
                blocks.Add(XenoMarkdown.Heading("Response body"));
                blocks.Add(Body(entry.ResponseBody));
            }

            return XenoMarkdown.Join(blocks);
        }

        private static KeyValuePair<string, string> Pair(string key, string value)
        {
            return new KeyValuePair<string, string>(key, value);
        }

        private static string Body(string? body)
        {
            if (String.IsNullOrEmpty(body)) return "_Not stored._";
            string trimmed = body.TrimStart();
            bool json = trimmed.StartsWith("{", StringComparison.Ordinal) || trimmed.StartsWith("[", StringComparison.Ordinal);
            return XenoMarkdown.CodeBlock(body, json ? "json" : null);
        }

        private static string Tokens(LocalRequestTelemetry telemetry)
        {
            if (telemetry.TotalTokens == null && telemetry.PromptTokens == null && telemetry.CompletionTokens == null) return DisplayFormat.Dash;
            return DisplayFormat.Number(telemetry.TotalTokens) + " total (" + DisplayFormat.Number(telemetry.PromptTokens) + " prompt, " + DisplayFormat.Number(telemetry.CompletionTokens) + " completion)";
        }
    }
}

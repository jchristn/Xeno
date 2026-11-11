namespace Xeno.Client.Presentation
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using Xeno.Client.Chat;
    using Xeno.Core.Chat;

    /// <summary>
    /// Words for chat values, shared by the desktop app, the terminal app, and the CLI.
    /// </summary>
    public static class ChatFormat
    {
        /// <summary>
        /// The reminder shown under the message box in every chat surface.
        /// </summary>
        public const string Disclaimer = "AI can make mistakes. Please check all work.";

        /// <summary>
        /// Where a turn ran, in words: "This machine (ollama)", "Xeno network", or "Unavailable".
        /// </summary>
        /// <param name="route">Route.</param>
        /// <param name="fallback">True for a network fallback.</param>
        /// <returns>Text, never null.</returns>
        public static string Route(string? route, bool fallback = false)
        {
            string text;
            if (String.IsNullOrWhiteSpace(route)) text = "-";
            else if (route.StartsWith("local:", StringComparison.OrdinalIgnoreCase)) text = "This machine (" + route.Substring(6) + ")";
            else if (String.Equals(route, "local", StringComparison.OrdinalIgnoreCase)) text = "This machine";
            else if (String.Equals(route, "network", StringComparison.OrdinalIgnoreCase)) text = "Xeno network";
            else if (String.Equals(route, "unavailable", StringComparison.OrdinalIgnoreCase)) text = "Unavailable";
            else text = route;
            return fallback ? text + " (fallback)" : text;
        }

        /// <summary>
        /// A duration: "850 ms", "2.4 s", or "-".
        /// </summary>
        /// <param name="ms">Milliseconds, or null.</param>
        /// <returns>Text.</returns>
        public static string Duration(double? ms)
        {
            if (!ms.HasValue) return "-";
            double value = ms.Value;
            return value < 1000 ? Math.Round(value).ToString(CultureInfo.InvariantCulture) + " ms" : (value / 1000).ToString(value < 10000 ? "0.0" : "0", CultureInfo.InvariantCulture) + " s";
        }

        /// <summary>
        /// A rating: "Thumbs up", "Thumbs down", or empty.
        /// </summary>
        /// <param name="rating">1, -1, or null.</param>
        /// <returns>Text.</returns>
        public static string Rating(int? rating)
        {
            return rating == 1 ? "Thumbs up" : rating == -1 ? "Thumbs down" : String.Empty;
        }

        /// <summary>
        /// An outcome: "Answered", "Failed", "Stopped", or "Running".
        /// </summary>
        /// <param name="status">Status.</param>
        /// <returns>Text.</returns>
        public static string Outcome(ChatTurnStatusEnum status)
        {
            switch (status)
            {
                case ChatTurnStatusEnum.Success: return "Answered";
                case ChatTurnStatusEnum.Error: return "Failed";
                case ChatTurnStatusEnum.Cancelled: return "Stopped";
                default: return "Running";
            }
        }

        /// <summary>
        /// Where a chat came from, in words.
        /// </summary>
        /// <param name="surface">Surface.</param>
        /// <returns>Text.</returns>
        public static string Surface(ChatSurfaceEnum surface)
        {
            switch (surface)
            {
                case ChatSurfaceEnum.Web: return "Web";
                case ChatSurfaceEnum.Desktop: return "Desktop";
                case ChatSurfaceEnum.Terminal: return "Terminal";
                case ChatSurfaceEnum.Cli: return "CLI";
                default: return "MCP";
            }
        }

        /// <summary>
        /// One line describing an answer: route, model, first token, total time, tokens.
        /// </summary>
        /// <param name="message">Assistant message.</param>
        /// <returns>Text.</returns>
        /// <exception cref="ArgumentNullException">message is null.</exception>
        public static string AnswerMeta(ChatMessage message)
        {
            ArgumentNullException.ThrowIfNull(message);
            string text = Route(message.Route, message.Fallback);
            if (!String.IsNullOrWhiteSpace(message.Model)) text += " · " + message.Model;
            if (message.TimeToFirstTokenMs.HasValue) text += " · first token " + Duration(message.TimeToFirstTokenMs);
            if (message.TotalMs.HasValue) text += " · " + Duration(message.TotalMs);
            if (message.CompletionTokens.HasValue) text += " · " + message.CompletionTokens.Value.ToString(CultureInfo.InvariantCulture) + " tokens";
            if (message.Incomplete && message.Failure == null) text += " · stopped early";
            return text;
        }

        /// <summary>
        /// The line every chat surface shows in the conversation when a model did not answer or stopped answering, for
        /// example "No response from the model: The model finished without writing anything.", or null when it answered.
        /// </summary>
        /// <param name="message">Assistant message.</param>
        /// <returns>Text, or null.</returns>
        /// <exception cref="ArgumentNullException">message is null.</exception>
        public static string? FailureLine(ChatMessage message)
        {
            ArgumentNullException.ThrowIfNull(message);
            if (message.Failure == null) return null;
            return message.Failure.Describe(!String.IsNullOrEmpty(message.Content) || !String.IsNullOrEmpty(message.Thinking) || message.ToolCalls.Count > 0);
        }

        /// <summary>
        /// The failure line for an error event while an answer streams.
        /// </summary>
        /// <param name="item">Error event.</param>
        /// <param name="partial">True when part of the answer already arrived.</param>
        /// <returns>Text.</returns>
        /// <exception cref="ArgumentNullException">item is null.</exception>
        public static string FailureLine(ChatStreamEvent item, bool partial)
        {
            ArgumentNullException.ThrowIfNull(item);
            if (item.Failure != null) return item.Failure.Describe(partial);
            return String.IsNullOrWhiteSpace(item.Text) ? ChatFailure.Headline(ChatFailureKindEnum.Failed, partial) + "." : item.Text!;
        }

        /// <summary>
        /// One line of answer telemetry for the terminal transcript, starting with an (i) marker: time to first token,
        /// streaming time, tool time, tokens in and out (with reasoning), and tokens per second.
        /// </summary>
        /// <param name="message">Assistant message.</param>
        /// <returns>Text.</returns>
        /// <exception cref="ArgumentNullException">message is null.</exception>
        public static string TelemetryLine(ChatMessage message)
        {
            ArgumentNullException.ThrowIfNull(message);
            ChatMessageStats? stats = message.Stats;
            List<string> parts = new List<string> { "(i) first token " + Duration(stats?.TimeToFirstTokenMs ?? message.TimeToFirstTokenMs) };
            if (stats?.StreamingMs != null) parts.Add("streaming " + Duration(stats.StreamingMs));
            parts.Add("model " + Duration(stats?.ModelMs ?? message.TotalMs));
            if (stats != null && stats.ToolCalls > 0) parts.Add("tools " + Duration(stats.ToolMs) + " (" + Count(stats.ToolCalls) + ")");
            parts.Add(Tokens(stats?.PromptTokens ?? message.PromptTokens) + " in / " + Tokens(stats?.CompletionTokens ?? message.CompletionTokens) + " out" + (stats?.ReasoningTokens != null ? " (" + Count(stats.ReasoningTokens.Value) + " reasoning)" : String.Empty));
            if (stats?.TokensPerSecond != null) parts.Add(stats.TokensPerSecond.Value.ToString("0.0", CultureInfo.InvariantCulture) + " tok/s");
            return String.Join(" · ", parts);
        }

        /// <summary>
        /// Telemetry for an answer as label and value pairs: route, model, time to first token, streaming time, model
        /// time, tool time and calls, model calls, turn time, tokens by type, and tokens per second.
        /// </summary>
        /// <param name="message">Assistant message.</param>
        /// <returns>Pairs, never null.</returns>
        /// <exception cref="ArgumentNullException">message is null.</exception>
        public static List<KeyValuePair<string, string>> Telemetry(ChatMessage message)
        {
            ArgumentNullException.ThrowIfNull(message);
            ChatMessageStats stats = message.Stats ?? new ChatMessageStats { Model = message.Model ?? String.Empty, Route = message.Route ?? String.Empty, TimeToFirstTokenMs = message.TimeToFirstTokenMs, ModelMs = message.TotalMs, PromptTokens = message.PromptTokens, CompletionTokens = message.CompletionTokens };
            List<KeyValuePair<string, string>> rows = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("Went to", Route(stats.Route, message.Fallback) + (String.IsNullOrWhiteSpace(stats.Provider) || stats.Route.Contains(stats.Provider, StringComparison.OrdinalIgnoreCase) ? String.Empty : " · " + stats.Provider)),
                new KeyValuePair<string, string>("Model", String.IsNullOrWhiteSpace(stats.Model) ? "-" : stats.Model),
                new KeyValuePair<string, string>("Streaming", stats.Streamed ? "on" : "off"),
                new KeyValuePair<string, string>("Time to first token", Duration(stats.TimeToFirstTokenMs)),
                new KeyValuePair<string, string>("Streaming time", Duration(stats.StreamingMs)),
                new KeyValuePair<string, string>("Model time", Duration(stats.ModelMs)),
                new KeyValuePair<string, string>("Tool time", stats.ToolCalls > 0 ? Duration(stats.ToolMs) + " (" + Count(stats.ToolCalls) + " calls)" : "none"),
                new KeyValuePair<string, string>("Model calls", Count(stats.ModelCalls)),
                new KeyValuePair<string, string>("Turn time", Duration(stats.TurnMs)),
                new KeyValuePair<string, string>("Prompt tokens", Tokens(stats.PromptTokens) + (stats.CachedPromptTokens.HasValue ? " (" + Count(stats.CachedPromptTokens.Value) + " cached)" : String.Empty)),
                new KeyValuePair<string, string>("Completion tokens", Tokens(stats.CompletionTokens) + (stats.ReasoningTokens.HasValue ? " (" + Count(stats.ReasoningTokens.Value) + " reasoning)" : String.Empty))
            };
            if (stats.ModelCalls > 1) rows.Add(new KeyValuePair<string, string>("Turn tokens", Tokens(stats.TurnPromptTokens) + " in, " + Tokens(stats.TurnCompletionTokens) + " out"));
            rows.Add(new KeyValuePair<string, string>("Tokens per second", stats.TokensPerSecond.HasValue ? stats.TokensPerSecond.Value.ToString("0.0", CultureInfo.InvariantCulture) : "-"));
            return rows;
        }

        private static string Tokens(int? value)
        {
            return value.HasValue ? Count(value.Value) : "-";
        }

        private static string Count(int value)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Short local time for a UTC timestamp.
        /// </summary>
        /// <param name="utc">UTC time.</param>
        /// <returns>Text, for example "11-10 14:05".</returns>
        public static string When(DateTime utc)
        {
            return DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime().ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);
        }
    }
}

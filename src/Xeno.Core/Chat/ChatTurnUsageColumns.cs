namespace Xeno.Core.Chat
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    /// <summary>
    /// Typed usage columns on chat turn tables (the local chat store and the server chat store), so usage analytics
    /// read numbers from columns rather than from the turn JSON.
    /// </summary>
    public static class ChatTurnUsageColumns
    {
        /// <summary>
        /// Current usage version written with every turn.
        /// </summary>
        public const int UsageVersion = 1;

        /// <summary>
        /// Columns holding milliseconds (floating point).
        /// </summary>
        public static readonly IReadOnlyList<string> RealColumns = new List<string> { "ttft_ms", "total_ms", "model_ms", "streaming_ms", "tool_ms", "approval_ms" };

        /// <summary>
        /// Columns holding counts (integers).
        /// </summary>
        public static readonly IReadOnlyList<string> IntegerColumns = new List<string> { "tool_call_count", "tool_failures", "tool_denied", "prompt_tokens", "cached_prompt_tokens", "completion_tokens", "usage_version" };

        /// <summary>
        /// Every usage column.
        /// </summary>
        public static IEnumerable<string> All
        {
            get
            {
                foreach (string column in RealColumns) yield return column;
                foreach (string column in IntegerColumns) yield return column;
            }
        }

        /// <summary>
        /// Parameter values for a turn, keyed by @column.
        /// </summary>
        /// <param name="turn">Turn.</param>
        /// <returns>Values.</returns>
        /// <exception cref="ArgumentNullException">turn is null.</exception>
        public static List<KeyValuePair<string, object?>> Values(ChatTurn turn)
        {
            ArgumentNullException.ThrowIfNull(turn);
            return new List<KeyValuePair<string, object?>>
            {
                new KeyValuePair<string, object?>("@ttft_ms", turn.TimeToFirstTokenMs),
                new KeyValuePair<string, object?>("@total_ms", turn.TotalMs),
                new KeyValuePair<string, object?>("@model_ms", turn.ModelMs),
                new KeyValuePair<string, object?>("@streaming_ms", turn.StreamingMs),
                new KeyValuePair<string, object?>("@tool_ms", turn.ToolMs),
                new KeyValuePair<string, object?>("@approval_ms", turn.ApprovalMs),
                new KeyValuePair<string, object?>("@tool_call_count", turn.ToolCallCount),
                new KeyValuePair<string, object?>("@tool_failures", turn.ToolFailures),
                new KeyValuePair<string, object?>("@tool_denied", turn.ToolDenied),
                new KeyValuePair<string, object?>("@prompt_tokens", turn.PromptTokens),
                new KeyValuePair<string, object?>("@cached_prompt_tokens", turn.CachedPromptTokens),
                new KeyValuePair<string, object?>("@completion_tokens", turn.CompletionTokens),
                new KeyValuePair<string, object?>("@usage_version", UsageVersion)
            };
        }

        /// <summary>
        /// "column = @column, ..." for an UPDATE.
        /// </summary>
        public static string SetList
        {
            get
            {
                List<string> sets = new List<string>();
                foreach (string column in All) sets.Add(column + " = @" + column);
                return String.Join(", ", sets);
            }
        }

        /// <summary>
        /// ", column, ..." for an INSERT column list.
        /// </summary>
        public static string InsertColumns
        {
            get { return ", " + String.Join(", ", All); }
        }

        /// <summary>
        /// ", @column, ..." for an INSERT value list.
        /// </summary>
        public static string InsertValues
        {
            get { return ", @" + String.Join(", @", All); }
        }

        /// <summary>
        /// Fill a turn's usage fields from column values read in <see cref="All"/> order.
        /// </summary>
        /// <param name="turn">Turn.</param>
        /// <param name="values">Values in <see cref="All"/> order; DBNull or null for missing.</param>
        /// <exception cref="ArgumentNullException">turn or values is null.</exception>
        public static void Read(ChatTurn turn, IReadOnlyList<object?> values)
        {
            ArgumentNullException.ThrowIfNull(turn);
            ArgumentNullException.ThrowIfNull(values);
            turn.TimeToFirstTokenMs = Real(values[0]);
            turn.TotalMs = Real(values[1]) ?? 0;
            turn.ModelMs = Real(values[2]) ?? 0;
            turn.StreamingMs = Real(values[3]);
            turn.ToolMs = Real(values[4]) ?? 0;
            turn.ApprovalMs = Real(values[5]) ?? 0;
            turn.ToolCallCount = Integer(values[6]) ?? 0;
            turn.ToolFailures = Integer(values[7]) ?? 0;
            turn.ToolDenied = Integer(values[8]) ?? 0;
            turn.PromptTokens = Integer(values[9]);
            turn.CachedPromptTokens = Integer(values[10]);
            turn.CompletionTokens = Integer(values[11]);
        }

        private static double? Real(object? value)
        {
            if (value == null || value is DBNull) return null;
            return Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }

        private static int? Integer(object? value)
        {
            if (value == null || value is DBNull) return null;
            return (int)Math.Clamp(Convert.ToInt64(value, CultureInfo.InvariantCulture), Int32.MinValue, Int32.MaxValue);
        }
    }
}

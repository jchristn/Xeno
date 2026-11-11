namespace Xeno.Core.Chat
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using Xeno.Core.Serialization;

    /// <summary>
    /// Writes and reads chat stream events as server-sent events: "event: &lt;kind&gt;" then "data: &lt;json&gt;".
    /// </summary>
    public static class ChatSse
    {
        /// <summary>
        /// Content type of a chat stream.
        /// </summary>
        public const string ContentType = "text/event-stream";

        /// <summary>
        /// Format one event.
        /// </summary>
        /// <param name="item">Event.</param>
        /// <returns>SSE text ending with a blank line.</returns>
        /// <exception cref="ArgumentNullException">item is null.</exception>
        public static string Format(ChatStreamEvent item)
        {
            ArgumentNullException.ThrowIfNull(item);
            return "event: " + EventName(item.Kind) + "\ndata: " + JsonUtil.Serialize(item) + "\n\n";
        }

        /// <summary>
        /// The SSE event name for a kind.
        /// </summary>
        /// <param name="kind">Kind.</param>
        /// <returns>Name, for example "chunk" or "tool_call".</returns>
        public static string EventName(ChatStreamEventKindEnum kind)
        {
            return kind == ChatStreamEventKindEnum.ToolCall ? "tool_call" : kind.ToString().ToLowerInvariant();
        }

        /// <summary>
        /// Read the events in a block of SSE text. Partial events at the end are left in <paramref name="buffer"/>.
        /// </summary>
        /// <param name="buffer">Text received so far; consumed events are removed.</param>
        /// <returns>Complete events, in order. Unreadable events are skipped.</returns>
        /// <exception cref="ArgumentNullException">buffer is null.</exception>
        public static List<ChatStreamEvent> Drain(StringBuilder buffer)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            List<ChatStreamEvent> events = new List<ChatStreamEvent>();
            while (true)
            {
                string text = buffer.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
                int end = text.IndexOf("\n\n", StringComparison.Ordinal);
                if (end < 0)
                {
                    buffer.Clear().Append(text);
                    return events;
                }

                string block = text.Substring(0, end);
                buffer.Clear().Append(text.Substring(end + 2));
                StringBuilder data = new StringBuilder();
                foreach (string line in block.Split('\n'))
                {
                    if (line.StartsWith("data:", StringComparison.Ordinal)) data.Append(line.Substring(5).TrimStart());
                }

                if (data.Length == 0) continue;
                try
                {
                    ChatStreamEvent? item = JsonUtil.Deserialize<ChatStreamEvent>(data.ToString());
                    if (item != null) events.Add(item);
                }
                catch (InvalidDataException)
                {
                }
            }
        }
    }
}

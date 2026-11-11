namespace Xeno.Client.Tools.Mcp
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Keeps the last few Voltaic log lines while a connection starts, to explain a failure. Only attached during
    /// connect, so tool arguments and results never pass through it.
    /// </summary>
    internal sealed class McpClientLogCapture
    {
        private const int MaxLines = 5;
        private const int MaxLineLength = 300;

        private readonly object _Lock = new object();
        private readonly Queue<string> _Lines = new Queue<string>();

        /// <summary>
        /// Log event handler.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="message">Message.</param>
        public void Capture(object? sender, string message)
        {
            if (String.IsNullOrWhiteSpace(message)) return;
            string line = message.Trim();
            if (line.Length > MaxLineLength) line = line.Substring(0, MaxLineLength) + "...";
            lock (_Lock)
            {
                _Lines.Enqueue(line);
                while (_Lines.Count > MaxLines) _Lines.Dequeue();
            }
        }

        /// <summary>
        /// The captured lines as a suffix for an error message, or empty.
        /// </summary>
        /// <returns>Text.</returns>
        public string Describe()
        {
            lock (_Lock)
            {
                if (_Lines.Count == 0) return String.Empty;
                return " " + String.Join(" | ", _Lines);
            }
        }
    }
}

namespace Xeno.Client.Tools.Workspace.Files
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    /// <summary>
    /// Line-ending and matching helpers shared by the file tools.
    /// </summary>
    internal static class FileToolText
    {
        /// <summary>
        /// Text with every line ending changed to LF.
        /// </summary>
        /// <param name="text">Text.</param>
        /// <returns>LF text.</returns>
        internal static string ToLf(string text)
        {
            return text.Replace("\r\n", "\n").Replace("\r", "\n");
        }

        /// <summary>
        /// The first line ending used in some text, or the platform default when it has none.
        /// </summary>
        /// <param name="content">Text.</param>
        /// <returns>Line ending.</returns>
        internal static string DetectLineEnding(string content)
        {
            int crlfIndex = content.IndexOf("\r\n", StringComparison.Ordinal);
            int lfIndex = content.IndexOf('\n');
            int crIndex = content.IndexOf('\r');
            if (crlfIndex >= 0 && (crlfIndex <= lfIndex || lfIndex < 0)) return "\r\n";
            if (lfIndex >= 0 && (crIndex < 0 || lfIndex < crIndex)) return "\n";
            if (crIndex >= 0) return "\r";
            return Environment.NewLine;
        }

        /// <summary>
        /// The first line ending used in an existing file, or the platform default.
        /// </summary>
        /// <param name="path">Full path.</param>
        /// <returns>Line ending.</returns>
        internal static string DetectFileLineEnding(string path)
        {
            using (StreamReader reader = new StreamReader(path, FileTextEncoding.Detect(path), detectEncodingFromByteOrderMarks: true))
            {
                int current;
                while ((current = reader.Read()) != -1)
                {
                    if (current == '\r') return reader.Read() == '\n' ? "\r\n" : "\r";
                    if (current == '\n') return "\n";
                }
            }

            return Environment.NewLine;
        }

        /// <summary>
        /// Start positions of every occurrence of a string, overlapping occurrences included.
        /// </summary>
        /// <param name="content">Text to search.</param>
        /// <param name="search">Non-empty text to find.</param>
        /// <returns>Positions.</returns>
        internal static List<int> FindAll(string content, string search)
        {
            List<int> positions = new List<int>();
            int index = 0;
            while (index < content.Length)
            {
                int found = content.IndexOf(search, index, StringComparison.Ordinal);
                if (found < 0) break;
                positions.Add(found);
                index = found + 1;
            }

            return positions;
        }

        /// <summary>
        /// The 1-based line number of a position in LF text.
        /// </summary>
        /// <param name="content">LF text.</param>
        /// <param name="position">Position.</param>
        /// <returns>Line number.</returns>
        internal static int LineOf(string content, int position)
        {
            int line = 1;
            for (int i = 0; i < position && i < content.Length; i++)
            {
                if (content[i] == '\n') line++;
            }

            return line;
        }

        /// <summary>
        /// The number of lines in LF text, counted the way Mux counts them.
        /// </summary>
        /// <param name="content">LF text.</param>
        /// <returns>Line count.</returns>
        internal static int LineCount(string content)
        {
            return content.Split('\n').Length;
        }

        /// <summary>
        /// A required string argument.
        /// </summary>
        /// <param name="value">Value.</param>
        /// <param name="name">Argument name.</param>
        /// <returns>Value.</returns>
        /// <exception cref="ArgumentException">The value is missing.</exception>
        internal static string Required(string? value, string name)
        {
            if (value == null) throw new ArgumentException("Required parameter '" + name + "' is missing or not a string.");
            return value;
        }

        /// <summary>
        /// A required, non-blank path argument.
        /// </summary>
        /// <param name="value">Value.</param>
        /// <param name="name">Argument name.</param>
        /// <returns>Value.</returns>
        /// <exception cref="ArgumentException">The value is missing or blank.</exception>
        internal static string RequiredPath(string? value, string name)
        {
            if (String.IsNullOrWhiteSpace(value)) throw new ArgumentException("Required parameter '" + name + "' is missing or empty.");
            return value;
        }
    }
}

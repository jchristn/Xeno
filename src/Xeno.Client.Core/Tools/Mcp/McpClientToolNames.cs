namespace Xeno.Client.Tools.Mcp
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;

    /// <summary>
    /// Builds the names MCP tools are offered under: mcp_&lt;server&gt;_&lt;tool&gt;, letters, digits, underscores, and
    /// hyphens only, at most 64 characters, unique.
    /// </summary>
    public static class McpClientToolNames
    {
        /// <summary>
        /// Longest name offered.
        /// </summary>
        public const int MaxLength = 64;

        /// <summary>
        /// Prefix of every name.
        /// </summary>
        public const string Prefix = "mcp_";

        /// <summary>
        /// Replace every character other than letters, digits, underscores, and hyphens with an underscore.
        /// </summary>
        /// <param name="value">Text.</param>
        /// <returns>Sanitized text.</returns>
        public static string Sanitize(string? value)
        {
            if (String.IsNullOrEmpty(value)) return "_";
            StringBuilder builder = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                bool allowed = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '-';
                builder.Append(allowed ? c : '_');
            }

            return builder.ToString();
        }

        /// <summary>
        /// Build a unique name, adding _2, _3, and so on when the name is taken (compared without case).
        /// </summary>
        /// <param name="server">Server name.</param>
        /// <param name="tool">Tool name on the server.</param>
        /// <param name="used">Names already offered; the new name is added.</param>
        /// <returns>Name.</returns>
        /// <exception cref="ArgumentNullException">used is null.</exception>
        public static string Build(string server, string tool, HashSet<string> used)
        {
            ArgumentNullException.ThrowIfNull(used);
            string name = Prefix + Sanitize(server) + "_" + Sanitize(tool);
            if (name.Length > MaxLength) name = name.Substring(0, MaxLength);
            string candidate = name;
            int counter = 2;
            while (!used.Add(candidate))
            {
                string suffix = "_" + counter.ToString(CultureInfo.InvariantCulture);
                candidate = name.Substring(0, Math.Min(name.Length, MaxLength - suffix.Length)) + suffix;
                counter++;
            }

            return candidate;
        }
    }
}

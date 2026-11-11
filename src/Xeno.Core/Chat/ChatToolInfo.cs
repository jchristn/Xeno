namespace Xeno.Core.Chat
{
    using System;

    /// <summary>
    /// A tool the chat model can use, as the chat tools list shows it.
    /// </summary>
    public sealed class ChatToolInfo
    {
        /// <summary>
        /// Name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// What it does.
        /// </summary>
        public string Description { get; set; } = String.Empty;

        /// <summary>
        /// True when it changes something, so the chat asks before running it.
        /// </summary>
        public bool Mutating { get; set; } = false;

        /// <summary>
        /// Group for display: Xeno, Files, Processes, Web, Planning, Memory, or MCP: &lt;server&gt;.
        /// </summary>
        public string Group { get; set; } = "Xeno";
    }
}

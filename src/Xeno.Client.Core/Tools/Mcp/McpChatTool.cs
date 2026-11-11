namespace Xeno.Client.Tools.Mcp
{
    using System;
    using Xeno.Core.Chat.Wire;

    /// <summary>
    /// A tool from an external MCP server as offered to the chat model.
    /// </summary>
    public sealed class McpChatTool
    {
        /// <summary>
        /// Name offered to the model: mcp_&lt;server&gt;_&lt;tool&gt;, letters, digits, underscores, and hyphens, at most 64 characters.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Server name from settings.
        /// </summary>
        public string Server { get; set; } = String.Empty;

        /// <summary>
        /// The tool's name on its server.
        /// </summary>
        public string ToolName { get; set; } = String.Empty;

        /// <summary>
        /// Description from the server.
        /// </summary>
        public string Description { get; set; } = String.Empty;

        /// <summary>
        /// Argument schema from the server.
        /// </summary>
        public ChatToolSchemaDto Schema { get; set; } = new ChatToolSchemaDto();

        /// <summary>
        /// True unless the server marks the tool read-only (annotations.readOnlyHint) or the server is trusted.
        /// </summary>
        public bool Mutating { get; set; } = true;
    }
}

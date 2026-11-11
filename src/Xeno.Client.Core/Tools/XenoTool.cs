namespace Xeno.Client.Tools
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Chat.Wire;

    /// <summary>
    /// One Xeno tool, offered both to MCP clients and to the in-app chat model.
    /// </summary>
    public sealed class XenoTool
    {
        /// <summary>
        /// Name, for example "xeno_status".
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// What the tool does, written for the model.
        /// </summary>
        public string Description { get; set; } = String.Empty;

        /// <summary>
        /// True when the tool changes something (settings, the client process, or sends a chat message), so the in-app
        /// chat asks before running it.
        /// </summary>
        public bool Mutating { get; set; } = false;

        /// <summary>
        /// JSON schema of the arguments.
        /// </summary>
        public ChatToolSchemaDto Schema { get; set; } = new ChatToolSchemaDto();

        /// <summary>
        /// Runs the tool with its arguments as JSON text and returns a result object to serialize.
        /// </summary>
        public Func<string, CancellationToken, Task<object>> Invoke { get; set; } = (arguments, token) => Task.FromResult<object>(new object());
    }
}

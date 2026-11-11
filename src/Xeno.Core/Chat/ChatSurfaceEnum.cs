namespace Xeno.Core.Chat
{
    /// <summary>
    /// The Xeno surface a chat turn came from.
    /// </summary>
    public enum ChatSurfaceEnum
    {
        /// <summary>
        /// Local web dashboard.
        /// </summary>
        Web,

        /// <summary>
        /// Desktop app.
        /// </summary>
        Desktop,

        /// <summary>
        /// Terminal app.
        /// </summary>
        Terminal,

        /// <summary>
        /// The xeno command line.
        /// </summary>
        Cli,

        /// <summary>
        /// An outside agent calling the xeno_chat MCP tool.
        /// </summary>
        Mcp,

        /// <summary>
        /// The Xeno server's web dashboard.
        /// </summary>
        Server
    }
}

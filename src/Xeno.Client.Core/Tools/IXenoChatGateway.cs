namespace Xeno.Client.Tools
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Sends a chat message on behalf of an outside agent (the xeno_chat tool) and returns the answer.
    /// </summary>
    public interface IXenoChatGateway
    {
        /// <summary>
        /// Ask a model something, in a new conversation or an existing one. The exchange is saved as a conversation
        /// whose surface is Mcp.
        /// </summary>
        /// <param name="arguments">Model, message, and optional conversation.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Answer.</returns>
        Task<XenoChatToolResult> AskAsync(XenoChatToolArguments arguments, CancellationToken token);
    }
}

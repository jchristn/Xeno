namespace Xeno.Core.Chat
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Chat.Wire;

    /// <summary>
    /// Tools the chat model may call.
    /// </summary>
    public interface IChatToolHost
    {
        /// <summary>
        /// Tool definitions offered to the model.
        /// </summary>
        IReadOnlyList<ChatCompletionToolDto> Definitions { get; }

        /// <summary>
        /// True when a tool changes something and must be approved before it runs. Unknown tools count as mutating.
        /// </summary>
        /// <param name="name">Tool name.</param>
        /// <returns>True when approval is needed.</returns>
        bool IsMutating(string name);

        /// <summary>
        /// Run a tool.
        /// </summary>
        /// <param name="name">Tool name.</param>
        /// <param name="arguments">Arguments as JSON text.</param>
        /// <param name="context">Conversation, turn, and working directory the call runs in.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Result as JSON text.</returns>
        /// <exception cref="System.ArgumentException">The tool is unknown or the arguments are invalid.</exception>
        Task<string> InvokeAsync(string name, string arguments, ChatToolContext context, CancellationToken token);
    }
}

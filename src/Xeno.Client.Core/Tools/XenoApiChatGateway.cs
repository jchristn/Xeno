namespace Xeno.Client.Tools
{
    using System;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Client.Chat;
    using Xeno.Core.Chat;

    /// <summary>
    /// Sends xeno_chat messages through the running client's chat API. Used by "xeno mcp", which runs in its own
    /// process; the client process uses its chat engine directly.
    /// </summary>
    public sealed class XenoApiChatGateway : IXenoChatGateway
    {
        private readonly XenoChatClient _Client;

        /// <summary>
        /// Create the gateway.
        /// </summary>
        /// <param name="client">Chat API client.</param>
        /// <exception cref="ArgumentNullException">client is null.</exception>
        public XenoApiChatGateway(XenoChatClient client)
        {
            _Client = client ?? throw new ArgumentNullException(nameof(client));
        }

        /// <inheritdoc />
        public async Task<XenoChatToolResult> AskAsync(XenoChatToolArguments arguments, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(arguments);
            string conversationId = arguments.ConversationId ?? String.Empty;
            if (String.IsNullOrWhiteSpace(conversationId))
            {
                if (String.IsNullOrWhiteSpace(arguments.Model)) throw new ArgumentException("model is required to start a conversation.");
                ChatConversation created = await _Client.CreateConversationAsync(new ChatConversationRequest { Model = arguments.Model, SystemPrompt = arguments.SystemPrompt, Surface = ChatSurfaceEnum.Mcp }, token).ConfigureAwait(false);
                conversationId = created.Id;
            }

            StringBuilder answer = new StringBuilder();
            string? error = null;
            ChatFailure? failure = null;
            ChatTurn? turn = await _Client.SendAsync(conversationId, new ChatSendRequest { Content = arguments.Message ?? String.Empty, Model = arguments.Model, Surface = ChatSurfaceEnum.Mcp }, item =>
            {
                if (item.Kind == ChatStreamEventKindEnum.Chunk) answer.Append(item.Text);
                if (item.Kind == ChatStreamEventKindEnum.Error)
                {
                    error = item.Text;
                    failure = item.Failure;
                }
                return Task.CompletedTask;
            }, token).ConfigureAwait(false);
            return XenoChatResults.Build(conversationId, turn, answer.ToString(), error, failure);
        }
    }
}

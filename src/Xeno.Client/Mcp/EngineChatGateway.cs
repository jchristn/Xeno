namespace Xeno.Client.Mcp
{
    using System;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Client.Chat;
    using Xeno.Core.Chat;
    using Xeno.Client.Tools;

    /// <summary>
    /// Sends xeno_chat messages through the chat engine in this process.
    /// </summary>
    public sealed class EngineChatGateway : IXenoChatGateway
    {
        private readonly IChatStore _Store;
        private readonly Func<ChatEngine> _Engine;
        private readonly Func<ClientSettings> _Settings;

        /// <summary>
        /// Create the gateway.
        /// </summary>
        /// <param name="store">Chat store.</param>
        /// <param name="engine">Returns the chat engine (it is created after the tool catalog that uses this gateway).</param>
        /// <param name="settings">Returns the current settings.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public EngineChatGateway(IChatStore store, Func<ChatEngine> engine, Func<ClientSettings> settings)
        {
            _Store = store ?? throw new ArgumentNullException(nameof(store));
            _Engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <inheritdoc />
        public async Task<XenoChatToolResult> AskAsync(XenoChatToolArguments arguments, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(arguments);
            string conversationId = arguments.ConversationId ?? String.Empty;
            if (String.IsNullOrWhiteSpace(conversationId))
            {
                if (String.IsNullOrWhiteSpace(arguments.Model)) throw new ArgumentException("model is required to start a conversation.");
                ChatConversation created = await _Store.CreateConversationAsync(new ChatConversation
                {
                    Model = arguments.Model.Trim(),
                    SystemPrompt = arguments.SystemPrompt ?? _Settings().Chat.DefaultSystemPrompt,
                    Surface = ChatSurfaceEnum.Mcp
                }, token).ConfigureAwait(false);
                conversationId = created.Id;
            }

            StringBuilder answer = new StringBuilder();
            string? error = null;
            ChatFailure? failure = null;
            ChatTurn turn = await _Engine().SendAsync(conversationId, new ChatSendRequest { Content = arguments.Message ?? String.Empty, Model = arguments.Model, Surface = ChatSurfaceEnum.Mcp }, item =>
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

namespace Xeno.Server.Routes
{
    using System;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;
    using Xeno.Core.Models;

    /// <summary>
    /// Route registrar for server chat (/v1.0/api/chat/...): models, conversations, streaming messages, cancel,
    /// feedback, history, and analytics. Handling is in <see cref="XenoServer.HandleChatAsync"/>.
    /// </summary>
    public class ChatRoutes
    {
        #region Private-Members

        private const string Tag = "Chat";
        private readonly XenoServer _Server;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the registrar.
        /// </summary>
        /// <param name="server">Owning server.</param>
        /// <exception cref="ArgumentNullException">server is null.</exception>
        public ChatRoutes(XenoServer server)
        {
            _Server = server ?? throw new ArgumentNullException(nameof(server));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Register the chat routes.
        /// </summary>
        /// <param name="server">Web server.</param>
        /// <param name="metadataFactory">OpenAPI metadata factory.</param>
        /// <param name="exceptionHandler">Shared route exception handler.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public void Register(
            Webserver server,
            Func<HttpMethod, string, string, string, OpenApiRouteMetadata> metadataFactory,
            Func<HttpContextBase, Exception, Task> exceptionHandler)
        {
            ArgumentNullException.ThrowIfNull(server);
            ArgumentNullException.ThrowIfNull(metadataFactory);
            ArgumentNullException.ThrowIfNull(exceptionHandler);
            Static(server, HttpMethod.GET, "/v1.0/api/chat/models", "List models for chat", "models", metadataFactory, exceptionHandler);
            Static(server, HttpMethod.POST, "/v1.0/api/chat/models/prepare", "Prepare a model for chat", "models.prepare", metadataFactory, exceptionHandler);
            Static(server, HttpMethod.GET, "/v1.0/api/chat/conversations", "List your chat conversations", "conversations.list", metadataFactory, exceptionHandler);
            Static(server, HttpMethod.POST, "/v1.0/api/chat/conversations", "Start a chat conversation", "conversations.create", metadataFactory, exceptionHandler);
            Static(server, HttpMethod.GET, "/v1.0/api/chat/feedback", "Query chat feedback (scope=tenant for every user, needs ChatAdmin)", "feedback.list", metadataFactory, exceptionHandler);
            Static(server, HttpMethod.GET, "/v1.0/api/chat/history", "Query chat history (scope=tenant for every user, needs ChatAdmin)", "history.list", metadataFactory, exceptionHandler);
            Static(server, HttpMethod.GET, "/v1.0/api/chat/analytics", "Chat analytics (scope=tenant for every user, needs ChatAdmin)", "analytics", metadataFactory, exceptionHandler);
            Static(server, HttpMethod.GET, "/v1.0/api/chat/analytics/usage", "Chat usage analytics (scope=tenant for every user, needs ChatAdmin)", "analytics.usage", metadataFactory, exceptionHandler);
            Parameter(server, HttpMethod.GET, "/v1.0/api/chat/conversations/{id}", "Read a chat conversation with its messages", "conversations.get", metadataFactory, exceptionHandler);
            Parameter(server, HttpMethod.PUT, "/v1.0/api/chat/conversations/{id}", "Change a chat conversation", "conversations.update", metadataFactory, exceptionHandler);
            Parameter(server, HttpMethod.DELETE, "/v1.0/api/chat/conversations/{id}", "Delete a chat conversation", "conversations.delete", metadataFactory, exceptionHandler);
            Parameter(server, HttpMethod.POST, "/v1.0/api/chat/conversations/{id}/messages", "Send a chat message; the answer streams as server-sent events", "messages.send", metadataFactory, exceptionHandler);
            Parameter(server, HttpMethod.POST, "/v1.0/api/chat/turns/{id}/cancel", "Stop a running chat turn", "turns.cancel", metadataFactory, exceptionHandler);
            Parameter(server, HttpMethod.PUT, "/v1.0/api/chat/messages/{id}/feedback", "Rate a chat answer", "feedback.set", metadataFactory, exceptionHandler);
            Parameter(server, HttpMethod.DELETE, "/v1.0/api/chat/messages/{id}/feedback", "Remove a chat answer's rating", "feedback.delete", metadataFactory, exceptionHandler);
            Parameter(server, HttpMethod.GET, "/v1.0/api/chat/history/{id}", "Read a chat turn", "history.get", metadataFactory, exceptionHandler);
        }

        #endregion

        #region Private-Methods

        private void Static(Webserver server, HttpMethod method, string path, string summary, string action, Func<HttpMethod, string, string, string, OpenApiRouteMetadata> metadataFactory, Func<HttpContextBase, Exception, Task> exceptionHandler)
        {
            server.Routes.PreAuthentication.Static.Add(method, path, Handler(action), exceptionHandler, openApiMetadata: metadataFactory(method, path, summary, Tag));
        }

        private void Parameter(Webserver server, HttpMethod method, string path, string summary, string action, Func<HttpMethod, string, string, string, OpenApiRouteMetadata> metadataFactory, Func<HttpContextBase, Exception, Task> exceptionHandler)
        {
            server.Routes.PreAuthentication.Parameter.Add(method, path, Handler(action), exceptionHandler, openApiMetadata: metadataFactory(method, path, summary, Tag));
        }

        private Func<HttpContextBase, Task> Handler(string action)
        {
            return async context =>
            {
                await _Server.ExecuteRouteAsync(context, async token =>
                {
                    RequestContext requestContext = await _Server.CreateRequestContextAsync(context, token).ConfigureAwait(false);
                    await _Server.HandleChatAsync(context, requestContext, action, token).ConfigureAwait(false);
                }).ConfigureAwait(false);
            };
        }

        #endregion
    }
}

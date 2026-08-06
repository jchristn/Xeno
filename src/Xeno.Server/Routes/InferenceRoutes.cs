namespace Xeno.Server.Routes
{
    using System;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;
    using Xeno.Core.Models;

    /// <summary>
    /// Route registrar for the OpenAI- and Ollama-compatible inference endpoints (model listing, chat and
    /// text completions, and embeddings) exposed under both the short provider-compatible aliases and the
    /// "/api/v1/openai/..." spellings. The registrar owns the route registrations and delegates request
    /// handling to the server's inference handlers, replacing the corresponding branches in the default
    /// dispatcher. Each handler is invoked through the shared route executor so authentication-context
    /// creation and streaming/exception behavior match the rest of the surface.
    /// </summary>
    public class InferenceRoutes
    {
        #region Private-Members

        private readonly XenoServer _Server;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the inference routes registrar.
        /// </summary>
        /// <param name="server">Owning server instance whose inference handlers are invoked.</param>
        public InferenceRoutes(XenoServer server)
        {
            _Server = server ?? throw new ArgumentNullException(nameof(server));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Register the inference routes on the supplied web server.
        /// </summary>
        /// <param name="server">Web server.</param>
        /// <param name="metadataFactory">Factory that builds OpenAPI metadata for a route.</param>
        /// <param name="exceptionHandler">Shared route exception handler.</param>
        public void Register(
            Webserver server,
            Func<HttpMethod, string, string, string, OpenApiRouteMetadata> metadataFactory,
            Func<HttpContextBase, Exception, Task> exceptionHandler)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            if (metadataFactory == null) throw new ArgumentNullException(nameof(metadataFactory));
            if (exceptionHandler == null) throw new ArgumentNullException(nameof(exceptionHandler));

            AddStatic(server, HttpMethod.GET, "/v1/models", "List OpenAI models", "OpenAI", ModelsListAsync, metadataFactory, exceptionHandler);
            AddStatic(server, HttpMethod.GET, "/api/v1/openai/v1/models", "List OpenAI models", "OpenAI", ModelsListAsync, metadataFactory, exceptionHandler);
            AddStatic(server, HttpMethod.POST, "/v1/chat/completions", "Create chat completion", "OpenAI", ChatCompletionsAsync, metadataFactory, exceptionHandler);
            AddStatic(server, HttpMethod.POST, "/v1/completions", "Create completion", "OpenAI", ChatCompletionsAsync, metadataFactory, exceptionHandler);
            AddStatic(server, HttpMethod.POST, "/api/v1/openai/v1/chat/completions", "Create chat completion", "OpenAI", ChatCompletionsAsync, metadataFactory, exceptionHandler);
            AddStatic(server, HttpMethod.POST, "/api/v1/openai/v1/completions", "Create completion", "OpenAI", ChatCompletionsAsync, metadataFactory, exceptionHandler);
            AddStatic(server, HttpMethod.POST, "/v1/embeddings", "Create embeddings", "OpenAI", EmbeddingsAsync, metadataFactory, exceptionHandler);
            AddStatic(server, HttpMethod.POST, "/api/v1/openai/v1/embeddings", "Create embeddings", "OpenAI", EmbeddingsAsync, metadataFactory, exceptionHandler);
            AddStatic(server, HttpMethod.GET, "/api/tags", "List Ollama models", "Ollama", TagsAsync, metadataFactory, exceptionHandler);
            AddStatic(server, HttpMethod.POST, "/api/chat", "Create Ollama chat completion", "Ollama", OllamaChatAsync, metadataFactory, exceptionHandler);
            AddStatic(server, HttpMethod.POST, "/api/generate", "Create Ollama generation", "Ollama", OllamaChatAsync, metadataFactory, exceptionHandler);
            AddStatic(server, HttpMethod.POST, "/api/embed", "Create Ollama embeddings", "Ollama", OllamaEmbedAsync, metadataFactory, exceptionHandler);
            AddStatic(server, HttpMethod.POST, "/api/embeddings", "Create Ollama embeddings", "Ollama", OllamaEmbedAsync, metadataFactory, exceptionHandler);
        }

        #endregion

        #region Private-Methods

        private void AddStatic(
            Webserver server,
            HttpMethod method,
            string path,
            string summary,
            string tag,
            Func<HttpContextBase, Task> handler,
            Func<HttpMethod, string, string, string, OpenApiRouteMetadata> metadataFactory,
            Func<HttpContextBase, Exception, Task> exceptionHandler)
        {
            server.Routes.PreAuthentication.Static.Add(method, path, handler, exceptionHandler, openApiMetadata: metadataFactory(method, path, summary, tag));
        }

        private async Task ModelsListAsync(HttpContextBase context)
        {
            await _Server.ExecuteRouteAsync(context, async token =>
            {
                RequestContext requestContext = await _Server.CreateRequestContextAsync(context, token).ConfigureAwait(false);
                await _Server.HandleOpenAiModelsListAsync(context, requestContext, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private async Task ChatCompletionsAsync(HttpContextBase context)
        {
            await _Server.ExecuteRouteAsync(context, async token =>
            {
                RequestContext requestContext = await _Server.CreateRequestContextAsync(context, token).ConfigureAwait(false);
                await _Server.HandleChatCompletionsRouteAsync(context, requestContext, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private async Task EmbeddingsAsync(HttpContextBase context)
        {
            await _Server.ExecuteRouteAsync(context, async token =>
            {
                RequestContext requestContext = await _Server.CreateRequestContextAsync(context, token).ConfigureAwait(false);
                await _Server.HandleOpenAiEmbeddingsRouteAsync(context, requestContext, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private async Task TagsAsync(HttpContextBase context)
        {
            await _Server.ExecuteRouteAsync(context, async token =>
            {
                RequestContext requestContext = await _Server.CreateRequestContextAsync(context, token).ConfigureAwait(false);
                await _Server.HandleOllamaTagsAsync(context, requestContext, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private async Task OllamaChatAsync(HttpContextBase context)
        {
            await _Server.ExecuteRouteAsync(context, async token =>
            {
                RequestContext requestContext = await _Server.CreateRequestContextAsync(context, token).ConfigureAwait(false);
                await _Server.HandleOllamaChatRouteAsync(context, requestContext, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private async Task OllamaEmbedAsync(HttpContextBase context)
        {
            await _Server.ExecuteRouteAsync(context, async token =>
            {
                RequestContext requestContext = await _Server.CreateRequestContextAsync(context, token).ConfigureAwait(false);
                await _Server.HandleOllamaEmbedRouteAsync(context, requestContext, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        #endregion
    }
}

namespace Xeno.Server.Routes
{
    using System;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;
    using Xeno.Core.Models;

    /// <summary>
    /// Route registrar for the client administration endpoints (list, delete/{id}, sharing/{id}, the shared
    /// clients admin view) and the managed client-model operations (delete, pull, and model-performance
    /// test). The registrar owns the route registrations and delegates request handling to the server's
    /// client handlers, replacing the corresponding branches in the default dispatcher. Each handler is
    /// invoked through the shared route executor so authentication-context creation and streaming/exception
    /// behavior match the rest of the surface.
    /// </summary>
    public class ClientRoutes
    {
        #region Private-Members

        private readonly XenoServer _Server;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the client routes registrar.
        /// </summary>
        /// <param name="server">Owning server instance whose client handlers are invoked.</param>
        public ClientRoutes(XenoServer server)
        {
            _Server = server ?? throw new ArgumentNullException(nameof(server));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Register the client routes on the supplied web server.
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

            AddStatic(server, HttpMethod.GET, "/api/v1/clients", "List clients", "Clients", ListAsync, metadataFactory, exceptionHandler);
            AddStatic(server, HttpMethod.GET, "/v1.0/api/clients", "List clients", "Clients", ListAsync, metadataFactory, exceptionHandler);
            AddStatic(server, HttpMethod.DELETE, "/api/v1/clientmodels", "Delete managed client model", "Clients", ModelDeleteAsync, metadataFactory, exceptionHandler);
            AddStatic(server, HttpMethod.DELETE, "/v1.0/api/client-models", "Delete managed client model", "Clients", ModelDeleteAsync, metadataFactory, exceptionHandler);
            AddStatic(server, HttpMethod.POST, "/api/v1/clientmodels/pull", "Pull managed client model", "Clients", ModelPullAsync, metadataFactory, exceptionHandler);
            AddStatic(server, HttpMethod.POST, "/v1.0/api/client-models/pull", "Pull managed client model", "Clients", ModelPullAsync, metadataFactory, exceptionHandler);
            AddStatic(server, HttpMethod.POST, "/api/v1/modelperformance/test", "Test model performance", "Clients", ModelPerformanceTestAsync, metadataFactory, exceptionHandler);
            AddStatic(server, HttpMethod.POST, "/v1.0/api/model-performance/test", "Test model performance", "Clients", ModelPerformanceTestAsync, metadataFactory, exceptionHandler);
            AddStatic(server, HttpMethod.GET, "/api/v1/admin/shared-clients", "List shared clients", "Clients", SharedClientsAdminAsync, metadataFactory, exceptionHandler);
            AddStatic(server, HttpMethod.GET, "/v1.0/api/admin/shared-clients", "List shared clients", "Clients", SharedClientsAdminAsync, metadataFactory, exceptionHandler);

            AddParameter(server, HttpMethod.DELETE, "/api/v1/clients/{id}", "Delete client", "Clients", DeleteAsync, metadataFactory, exceptionHandler);
            AddParameter(server, HttpMethod.DELETE, "/v1.0/api/clients/{id}", "Delete client", "Clients", DeleteAsync, metadataFactory, exceptionHandler);
            AddParameter(server, HttpMethod.POST, "/api/v1/clients/{id}/sharing", "Set client sharing", "Clients", SharingAsync, metadataFactory, exceptionHandler);
            AddParameter(server, HttpMethod.POST, "/v1.0/api/clients/{id}/sharing", "Set client sharing", "Clients", SharingAsync, metadataFactory, exceptionHandler);
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

        private void AddParameter(
            Webserver server,
            HttpMethod method,
            string path,
            string summary,
            string tag,
            Func<HttpContextBase, Task> handler,
            Func<HttpMethod, string, string, string, OpenApiRouteMetadata> metadataFactory,
            Func<HttpContextBase, Exception, Task> exceptionHandler)
        {
            server.Routes.PreAuthentication.Parameter.Add(method, path, handler, exceptionHandler, openApiMetadata: metadataFactory(method, path, summary, tag));
        }

        private async Task ListAsync(HttpContextBase context)
        {
            await _Server.ExecuteRouteAsync(context, async token =>
            {
                RequestContext requestContext = await _Server.CreateRequestContextAsync(context, token).ConfigureAwait(false);
                await _Server.HandleClientsListAsync(context, requestContext, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private async Task DeleteAsync(HttpContextBase context)
        {
            await _Server.ExecuteRouteAsync(context, async token =>
            {
                RequestContext requestContext = await _Server.CreateRequestContextAsync(context, token).ConfigureAwait(false);
                await _Server.HandleClientDeleteRouteAsync(context, requestContext, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private async Task SharingAsync(HttpContextBase context)
        {
            await _Server.ExecuteRouteAsync(context, async token =>
            {
                RequestContext requestContext = await _Server.CreateRequestContextAsync(context, token).ConfigureAwait(false);
                await _Server.HandleClientSharingSetAsync(context, requestContext, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private async Task SharedClientsAdminAsync(HttpContextBase context)
        {
            await _Server.ExecuteRouteAsync(context, async token =>
            {
                RequestContext requestContext = await _Server.CreateRequestContextAsync(context, token).ConfigureAwait(false);
                await _Server.HandleSharedClientsAdminAsync(context, requestContext, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private async Task ModelDeleteAsync(HttpContextBase context)
        {
            await _Server.ExecuteRouteAsync(context, async token =>
            {
                RequestContext requestContext = await _Server.CreateRequestContextAsync(context, token).ConfigureAwait(false);
                await _Server.HandleClientModelDeleteRouteAsync(context, requestContext, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private async Task ModelPullAsync(HttpContextBase context)
        {
            await _Server.ExecuteRouteAsync(context, async token =>
            {
                RequestContext requestContext = await _Server.CreateRequestContextAsync(context, token).ConfigureAwait(false);
                await _Server.HandleClientModelPullRouteAsync(context, requestContext, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private async Task ModelPerformanceTestAsync(HttpContextBase context)
        {
            await _Server.ExecuteRouteAsync(context, async token =>
            {
                RequestContext requestContext = await _Server.CreateRequestContextAsync(context, token).ConfigureAwait(false);
                await _Server.HandleModelPerformanceTestRouteAsync(context, requestContext, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        #endregion
    }
}

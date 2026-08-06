namespace Xeno.Server.Routes
{
    using System;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;
    using Xeno.Core.Models;

    /// <summary>
    /// Route registrar for the authenticated user administration endpoints (list, create, update, and
    /// delete). Delegates request handling to the server's user handlers through the shared route executor.
    /// </summary>
    public class UserRoutes
    {
        #region Private-Members

        private readonly XenoServer _Server;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the user routes registrar.
        /// </summary>
        /// <param name="server">Owning server instance whose user handlers are invoked.</param>
        public UserRoutes(XenoServer server)
        {
            _Server = server ?? throw new ArgumentNullException(nameof(server));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Register the user routes on the supplied web server.
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

            AddStatic(server, HttpMethod.GET, "/api/v1/users", "List users", "Tenants", ListAsync, metadataFactory, exceptionHandler);
            AddStatic(server, HttpMethod.GET, "/v1.0/api/users", "List users", "Tenants", ListAsync, metadataFactory, exceptionHandler);
            AddStatic(server, HttpMethod.POST, "/api/v1/users", "Create user", "Tenants", CreateAsync, metadataFactory, exceptionHandler);
            AddStatic(server, HttpMethod.POST, "/v1.0/api/users", "Create user", "Tenants", CreateAsync, metadataFactory, exceptionHandler);

            AddParameter(server, HttpMethod.PUT, "/api/v1/users/{id}", "Update user", "Tenants", UpdateAsync, metadataFactory, exceptionHandler);
            AddParameter(server, HttpMethod.PUT, "/v1.0/api/users/{id}", "Update user", "Tenants", UpdateAsync, metadataFactory, exceptionHandler);
            AddParameter(server, HttpMethod.DELETE, "/api/v1/users/{id}", "Delete user", "Tenants", DeleteAsync, metadataFactory, exceptionHandler);
            AddParameter(server, HttpMethod.DELETE, "/v1.0/api/users/{id}", "Delete user", "Tenants", DeleteAsync, metadataFactory, exceptionHandler);
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
                await _Server.HandleUsersListAsync(context, requestContext, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private async Task CreateAsync(HttpContextBase context)
        {
            await _Server.ExecuteRouteAsync(context, async token =>
            {
                RequestContext requestContext = await _Server.CreateRequestContextAsync(context, token).ConfigureAwait(false);
                await _Server.HandleUserCreateAsync(context, requestContext, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private async Task UpdateAsync(HttpContextBase context)
        {
            await _Server.ExecuteRouteAsync(context, async token =>
            {
                RequestContext requestContext = await _Server.CreateRequestContextAsync(context, token).ConfigureAwait(false);
                await _Server.HandleUserUpdateAsync(context, requestContext, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private async Task DeleteAsync(HttpContextBase context)
        {
            await _Server.ExecuteRouteAsync(context, async token =>
            {
                RequestContext requestContext = await _Server.CreateRequestContextAsync(context, token).ConfigureAwait(false);
                await _Server.HandleUserDeleteAsync(context, requestContext, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        #endregion
    }
}

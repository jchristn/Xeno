namespace Xeno.Server.Routes
{
    using System;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;
    using Xeno.Core.Models;

    /// <summary>
    /// Route registrar for the tenant permission administration endpoints (list, grantable list, create,
    /// update, and delete). Delegates request handling to the server's permission handlers through the shared
    /// route executor.
    /// </summary>
    public class PermissionRoutes
    {
        #region Private-Members

        private readonly XenoServer _Server;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the permission routes registrar.
        /// </summary>
        /// <param name="server">Owning server instance whose permission handlers are invoked.</param>
        public PermissionRoutes(XenoServer server)
        {
            _Server = server ?? throw new ArgumentNullException(nameof(server));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Register the permission routes on the supplied web server.
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

            AddStatic(server, HttpMethod.GET, "/api/v1/permissions", "List permissions", "Authorization", ListAsync, metadataFactory, exceptionHandler);
            AddStatic(server, HttpMethod.GET, "/v1.0/api/permissions", "List permissions", "Authorization", ListAsync, metadataFactory, exceptionHandler);
            AddStatic(server, HttpMethod.GET, "/api/v1/permissions/grantable", "List grantable permissions", "Authorization", GrantableAsync, metadataFactory, exceptionHandler);
            AddStatic(server, HttpMethod.GET, "/v1.0/api/permissions/grantable", "List grantable permissions", "Authorization", GrantableAsync, metadataFactory, exceptionHandler);
            AddStatic(server, HttpMethod.POST, "/api/v1/permissions", "Create permission", "Authorization", CreateAsync, metadataFactory, exceptionHandler);
            AddStatic(server, HttpMethod.POST, "/v1.0/api/permissions", "Create permission", "Authorization", CreateAsync, metadataFactory, exceptionHandler);

            AddParameter(server, HttpMethod.PUT, "/api/v1/permissions/{id}", "Update permission", "Authorization", UpdateAsync, metadataFactory, exceptionHandler);
            AddParameter(server, HttpMethod.PUT, "/v1.0/api/permissions/{id}", "Update permission", "Authorization", UpdateAsync, metadataFactory, exceptionHandler);
            AddParameter(server, HttpMethod.DELETE, "/api/v1/permissions/{id}", "Delete permission", "Authorization", DeleteAsync, metadataFactory, exceptionHandler);
            AddParameter(server, HttpMethod.DELETE, "/v1.0/api/permissions/{id}", "Delete permission", "Authorization", DeleteAsync, metadataFactory, exceptionHandler);
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
                await _Server.HandlePermissionsListAsync(context, requestContext, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private async Task GrantableAsync(HttpContextBase context)
        {
            await _Server.ExecuteRouteAsync(context, async token =>
            {
                RequestContext requestContext = await _Server.CreateRequestContextAsync(context, token).ConfigureAwait(false);
                await _Server.HandlePermissionsGrantableAsync(context, requestContext, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private async Task CreateAsync(HttpContextBase context)
        {
            await _Server.ExecuteRouteAsync(context, async token =>
            {
                RequestContext requestContext = await _Server.CreateRequestContextAsync(context, token).ConfigureAwait(false);
                await _Server.HandlePermissionCreateAsync(context, requestContext, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private async Task UpdateAsync(HttpContextBase context)
        {
            await _Server.ExecuteRouteAsync(context, async token =>
            {
                RequestContext requestContext = await _Server.CreateRequestContextAsync(context, token).ConfigureAwait(false);
                await _Server.HandlePermissionUpdateAsync(context, requestContext, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private async Task DeleteAsync(HttpContextBase context)
        {
            await _Server.ExecuteRouteAsync(context, async token =>
            {
                RequestContext requestContext = await _Server.CreateRequestContextAsync(context, token).ConfigureAwait(false);
                await _Server.HandlePermissionDeleteAsync(context, requestContext, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        #endregion
    }
}

namespace Xeno.Server.Routes
{
    using System;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>
    /// Route registrar for the unauthenticated authentication endpoints (login, tenant lookup, and
    /// dashboard login). The registrar owns the route registrations and delegates request handling to the
    /// server's authentication handlers, replacing the corresponding branches in the default dispatcher.
    /// </summary>
    public class AuthRoutes
    {
        #region Private-Members

        private readonly XenoServer _Server;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the auth routes registrar.
        /// </summary>
        /// <param name="server">Owning server instance whose authentication handlers are invoked.</param>
        public AuthRoutes(XenoServer server)
        {
            _Server = server ?? throw new ArgumentNullException(nameof(server));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Register the authentication routes on the supplied web server.
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

            Add(server, HttpMethod.POST, "/api/v1/auth/login", "Login and create client credential", "Authentication", LoginAsync, metadataFactory, exceptionHandler);
            Add(server, HttpMethod.POST, "/v1.0/api/auth/login", "Login and create client credential", "Authentication", LoginAsync, metadataFactory, exceptionHandler);

            Add(server, HttpMethod.GET, "/api/v1/auth/tenants", "List login tenants for email", "Authentication", TenantLookupAsync, metadataFactory, exceptionHandler);
            Add(server, HttpMethod.GET, "/v1.0/api/auth/tenants", "List login tenants for email", "Authentication", TenantLookupAsync, metadataFactory, exceptionHandler);
            Add(server, HttpMethod.POST, "/api/v1/auth/tenants", "List login tenants for email", "Authentication", TenantLookupAsync, metadataFactory, exceptionHandler);
            Add(server, HttpMethod.POST, "/v1.0/api/auth/tenants", "List login tenants for email", "Authentication", TenantLookupAsync, metadataFactory, exceptionHandler);

            Add(server, HttpMethod.POST, "/api/v1/auth/dashboard/login", "Dashboard login", "Authentication", DashboardLoginAsync, metadataFactory, exceptionHandler);
            Add(server, HttpMethod.POST, "/v1.0/api/auth/dashboard/login", "Dashboard login", "Authentication", DashboardLoginAsync, metadataFactory, exceptionHandler);
        }

        #endregion

        #region Private-Methods

        private void Add(
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

        private async Task LoginAsync(HttpContextBase context)
        {
            await _Server.ExecuteRouteAsync(context, async token =>
            {
                if (await _Server.RejectRateLimitedAuthAttemptAsync(context).ConfigureAwait(false)) return;
                await _Server.HandleLoginAsync(context, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private async Task TenantLookupAsync(HttpContextBase context)
        {
            await _Server.ExecuteRouteAsync(context, token => _Server.HandleLoginTenantLookupAsync(context, token)).ConfigureAwait(false);
        }

        private async Task DashboardLoginAsync(HttpContextBase context)
        {
            await _Server.ExecuteRouteAsync(context, async token =>
            {
                if (await _Server.RejectRateLimitedAuthAttemptAsync(context).ConfigureAwait(false)) return;
                await _Server.HandleDashboardLoginAsync(context, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        #endregion
    }
}

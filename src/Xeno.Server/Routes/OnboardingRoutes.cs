namespace Xeno.Server.Routes
{
    using System;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>
    /// Route registrar for the unauthenticated onboarding endpoints (sign-up, verify, and config). The
    /// registrar owns the route registrations and delegates request handling to the server's onboarding
    /// handlers, replacing the corresponding branches in the default dispatcher.
    /// </summary>
    public class OnboardingRoutes
    {
        #region Private-Members

        private readonly XenoServer _Server;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the onboarding routes registrar.
        /// </summary>
        /// <param name="server">Owning server instance whose onboarding handlers are invoked.</param>
        public OnboardingRoutes(XenoServer server)
        {
            _Server = server ?? throw new ArgumentNullException(nameof(server));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Register the onboarding routes on the supplied web server.
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

            Add(server, HttpMethod.POST, "/api/v1/onboarding/signup", "Onboard tenant or user", "Onboarding", SignupAsync, metadataFactory, exceptionHandler);
            Add(server, HttpMethod.POST, "/v1.0/api/onboarding/signup", "Onboard tenant or user", "Onboarding", SignupAsync, metadataFactory, exceptionHandler);

            Add(server, HttpMethod.GET, "/api/v1/onboarding/verify", "Verify onboarding email", "Onboarding", VerifyAsync, metadataFactory, exceptionHandler);
            Add(server, HttpMethod.GET, "/v1.0/api/onboarding/verify", "Verify onboarding email", "Onboarding", VerifyAsync, metadataFactory, exceptionHandler);
            Add(server, HttpMethod.POST, "/api/v1/onboarding/verify", "Verify onboarding email", "Onboarding", VerifyAsync, metadataFactory, exceptionHandler);
            Add(server, HttpMethod.POST, "/v1.0/api/onboarding/verify", "Verify onboarding email", "Onboarding", VerifyAsync, metadataFactory, exceptionHandler);

            Add(server, HttpMethod.GET, "/api/v1/onboarding/config", "Onboarding configuration", "Onboarding", ConfigAsync, metadataFactory, exceptionHandler);
            Add(server, HttpMethod.GET, "/v1.0/api/onboarding/config", "Onboarding configuration", "Onboarding", ConfigAsync, metadataFactory, exceptionHandler);
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

        private async Task SignupAsync(HttpContextBase context)
        {
            await _Server.ExecuteRouteAsync(context, async token =>
            {
                if (await _Server.RejectRateLimitedAuthAttemptAsync(context).ConfigureAwait(false)) return;
                await _Server.HandleOnboardingSignupAsync(context, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private async Task VerifyAsync(HttpContextBase context)
        {
            await _Server.ExecuteRouteAsync(context, token => _Server.HandleOnboardingVerifyAsync(context, token)).ConfigureAwait(false);
        }

        private async Task ConfigAsync(HttpContextBase context)
        {
            await _Server.ExecuteRouteAsync(context, token => _Server.HandleOnboardingConfigAsync(context, token)).ConfigureAwait(false);
        }

        #endregion
    }
}

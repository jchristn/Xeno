namespace Xeno.Server.Routes
{
    using System;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;
    using Xeno.Core.Models;

    /// <summary>
    /// Route registrar for the ledger endpoints (status, balance/me, balances, balances/users/{userId},
    /// entries/me, entries/search, drain, and reconcile). The registrar owns the route registrations and
    /// delegates request handling to the server's ledger handlers, replacing the corresponding branches in
    /// the default dispatcher. Each handler is invoked through the shared route executor so
    /// authentication-context creation and exception-to-status mapping match the rest of the surface.
    /// </summary>
    public class LedgerRoutes
    {
        #region Private-Members

        private readonly XenoServer _Server;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the ledger routes registrar.
        /// </summary>
        /// <param name="server">Owning server instance whose ledger handlers are invoked.</param>
        public LedgerRoutes(XenoServer server)
        {
            _Server = server ?? throw new ArgumentNullException(nameof(server));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Register the ledger routes on the supplied web server.
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

            AddStatic(server, HttpMethod.GET, "/v1.0/api/ledger/status", "Ledger status", "Ledger", StatusAsync, metadataFactory, exceptionHandler);
            AddStatic(server, HttpMethod.GET, "/v1.0/api/ledger/balance/me", "My ledger balance", "Ledger", BalanceMeAsync, metadataFactory, exceptionHandler);
            AddStatic(server, HttpMethod.GET, "/v1.0/api/ledger/balances", "Ledger balances", "Ledger", BalancesAsync, metadataFactory, exceptionHandler);
            AddStatic(server, HttpMethod.GET, "/v1.0/api/ledger/entries/me", "My ledger entries", "Ledger", EntriesMeAsync, metadataFactory, exceptionHandler);
            AddStatic(server, HttpMethod.POST, "/v1.0/api/ledger/entries/search", "Search ledger entries", "Ledger", EntriesSearchAsync, metadataFactory, exceptionHandler);
            AddStatic(server, HttpMethod.POST, "/v1.0/api/ledger/drain", "Drain ledger queue", "Ledger", DrainAsync, metadataFactory, exceptionHandler);
            AddStatic(server, HttpMethod.POST, "/v1.0/api/ledger/reconcile", "Reconcile ledger links", "Ledger", ReconcileAsync, metadataFactory, exceptionHandler);

            AddParameter(server, HttpMethod.GET, "/v1.0/api/ledger/balances/users/{userId}", "Ledger user balance", "Ledger", BalanceUserAsync, metadataFactory, exceptionHandler);
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

        private async Task StatusAsync(HttpContextBase context)
        {
            await _Server.ExecuteRouteAsync(context, async token =>
            {
                RequestContext requestContext = await _Server.CreateRequestContextAsync(context, token).ConfigureAwait(false);
                await _Server.HandleLedgerStatusAsync(context, requestContext, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private async Task BalanceMeAsync(HttpContextBase context)
        {
            await _Server.ExecuteRouteAsync(context, async token =>
            {
                RequestContext requestContext = await _Server.CreateRequestContextAsync(context, token).ConfigureAwait(false);
                await _Server.HandleLedgerBalanceMeAsync(context, requestContext, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private async Task BalancesAsync(HttpContextBase context)
        {
            await _Server.ExecuteRouteAsync(context, async token =>
            {
                RequestContext requestContext = await _Server.CreateRequestContextAsync(context, token).ConfigureAwait(false);
                await _Server.HandleLedgerBalancesAsync(context, requestContext, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private async Task BalanceUserAsync(HttpContextBase context)
        {
            await _Server.ExecuteRouteAsync(context, async token =>
            {
                RequestContext requestContext = await _Server.CreateRequestContextAsync(context, token).ConfigureAwait(false);
                await _Server.HandleLedgerBalanceUserAsync(context, requestContext, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private async Task EntriesMeAsync(HttpContextBase context)
        {
            await _Server.ExecuteRouteAsync(context, async token =>
            {
                RequestContext requestContext = await _Server.CreateRequestContextAsync(context, token).ConfigureAwait(false);
                await _Server.HandleLedgerEntriesMeAsync(context, requestContext, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private async Task EntriesSearchAsync(HttpContextBase context)
        {
            await _Server.ExecuteRouteAsync(context, async token =>
            {
                RequestContext requestContext = await _Server.CreateRequestContextAsync(context, token).ConfigureAwait(false);
                await _Server.HandleLedgerEntriesSearchAsync(context, requestContext, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private async Task DrainAsync(HttpContextBase context)
        {
            await _Server.ExecuteRouteAsync(context, async token =>
            {
                RequestContext requestContext = await _Server.CreateRequestContextAsync(context, token).ConfigureAwait(false);
                await _Server.HandleLedgerDrainAsync(context, requestContext, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private async Task ReconcileAsync(HttpContextBase context)
        {
            await _Server.ExecuteRouteAsync(context, async token =>
            {
                RequestContext requestContext = await _Server.CreateRequestContextAsync(context, token).ConfigureAwait(false);
                await _Server.HandleLedgerReconcileAsync(context, requestContext, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        #endregion
    }
}

namespace Xeno.Server.Routes
{
    using System;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;
    using Xeno.Core.Models;

    /// <summary>
    /// Route registrar for the authenticated client-telemetry endpoints (host and provider telemetry
    /// status, latest samples, collection triggers, history search/rollups/delete, collection attempts,
    /// metric catalog, and per-sample detail get/delete). The registrar owns the route registrations and
    /// delegates request handling to the server's telemetry handlers, replacing the corresponding branches
    /// in the default dispatcher.
    /// </summary>
    public class ClientTelemetryRoutes
    {
        #region Private-Members

        private readonly XenoServer _Server;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the client-telemetry routes registrar.
        /// </summary>
        /// <param name="server">Owning server instance whose telemetry handlers are invoked.</param>
        public ClientTelemetryRoutes(XenoServer server)
        {
            _Server = server ?? throw new ArgumentNullException(nameof(server));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Register the client-telemetry routes on the supplied web server.
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

            AddStatic(server, HttpMethod.GET, "/v1.0/api/client-telemetry/status", "Client telemetry status", "Client Telemetry", StatusAsync, metadataFactory, exceptionHandler);
            AddStatic(server, HttpMethod.GET, "/v1.0/api/client-telemetry/latest", "Latest client telemetry", "Client Telemetry", LatestAsync, metadataFactory, exceptionHandler);
            AddStatic(server, HttpMethod.POST, "/v1.0/api/client-telemetry/collect", "Trigger client telemetry collection", "Client Telemetry", CollectAsync, metadataFactory, exceptionHandler);
            AddStatic(server, HttpMethod.POST, "/v1.0/api/client-telemetry/history/search", "Search client telemetry history", "Client Telemetry", HistorySearchAsync, metadataFactory, exceptionHandler);
            AddStatic(server, HttpMethod.POST, "/v1.0/api/client-telemetry/history/rollups", "Client telemetry rollups", "Client Telemetry", HistoryRollupsAsync, metadataFactory, exceptionHandler);
            AddStatic(server, HttpMethod.POST, "/v1.0/api/client-telemetry/history/delete", "Bulk delete client telemetry", "Client Telemetry", HistoryBulkDeleteAsync, metadataFactory, exceptionHandler);
            AddStatic(server, HttpMethod.GET, "/v1.0/api/client-telemetry/providers/latest", "Latest provider telemetry", "Client Telemetry", ProvidersLatestAsync, metadataFactory, exceptionHandler);
            AddStatic(server, HttpMethod.POST, "/v1.0/api/client-telemetry/providers/collect", "Trigger provider telemetry collection", "Client Telemetry", CollectAsync, metadataFactory, exceptionHandler);
            AddStatic(server, HttpMethod.POST, "/v1.0/api/client-telemetry/providers/history/search", "Search provider telemetry history", "Client Telemetry", ProvidersHistorySearchAsync, metadataFactory, exceptionHandler);
            AddStatic(server, HttpMethod.POST, "/v1.0/api/client-telemetry/providers/history/rollups", "Provider telemetry rollups", "Client Telemetry", ProvidersHistoryRollupsAsync, metadataFactory, exceptionHandler);
            AddStatic(server, HttpMethod.POST, "/v1.0/api/client-telemetry/providers/history/delete", "Bulk delete provider telemetry", "Client Telemetry", ProvidersHistoryBulkDeleteAsync, metadataFactory, exceptionHandler);
            AddStatic(server, HttpMethod.GET, "/v1.0/api/client-telemetry/collection-attempts", "List client telemetry collection attempts", "Client Telemetry", CollectionAttemptsAsync, metadataFactory, exceptionHandler);
            AddStatic(server, HttpMethod.GET, "/v1.0/api/client-telemetry/providers/health", "Provider health strip", "Client Telemetry", ProvidersHealthAsync, metadataFactory, exceptionHandler);
            AddStatic(server, HttpMethod.GET, "/v1.0/api/client-telemetry/connected-history", "Connected client history", "Client Telemetry", ConnectedHistoryAsync, metadataFactory, exceptionHandler);
            AddStatic(server, HttpMethod.GET, "/v1.0/api/client-telemetry/metrics", "Client telemetry metric catalog", "Client Telemetry", MetricsAsync, metadataFactory, exceptionHandler);

            AddParameter(server, HttpMethod.GET, "/v1.0/api/client-telemetry/history/{id}", "Get client telemetry detail", "Client Telemetry", HistoryGetAsync, metadataFactory, exceptionHandler);
            AddParameter(server, HttpMethod.DELETE, "/v1.0/api/client-telemetry/history/{id}", "Delete client telemetry detail", "Client Telemetry", HistoryDeleteAsync, metadataFactory, exceptionHandler);
            AddParameter(server, HttpMethod.GET, "/v1.0/api/client-telemetry/providers/history/{id}", "Get provider telemetry detail", "Client Telemetry", ProvidersHistoryGetAsync, metadataFactory, exceptionHandler);
            AddParameter(server, HttpMethod.DELETE, "/v1.0/api/client-telemetry/providers/history/{id}", "Delete provider telemetry detail", "Client Telemetry", ProvidersHistoryDeleteAsync, metadataFactory, exceptionHandler);
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
                await _Server.HandleClientTelemetryStatusAsync(context, requestContext, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private async Task LatestAsync(HttpContextBase context)
        {
            await _Server.ExecuteRouteAsync(context, async token =>
            {
                RequestContext requestContext = await _Server.CreateRequestContextAsync(context, token).ConfigureAwait(false);
                await _Server.HandleClientTelemetryLatestAsync(context, requestContext, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private async Task ProvidersLatestAsync(HttpContextBase context)
        {
            await _Server.ExecuteRouteAsync(context, async token =>
            {
                RequestContext requestContext = await _Server.CreateRequestContextAsync(context, token).ConfigureAwait(false);
                await _Server.HandleProviderTelemetryLatestAsync(context, requestContext, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private async Task CollectAsync(HttpContextBase context)
        {
            await _Server.ExecuteRouteAsync(context, async token =>
            {
                RequestContext requestContext = await _Server.CreateRequestContextAsync(context, token).ConfigureAwait(false);
                await _Server.HandleClientTelemetryCollectAsync(context, requestContext, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private async Task HistorySearchAsync(HttpContextBase context)
        {
            await _Server.ExecuteRouteAsync(context, async token =>
            {
                RequestContext requestContext = await _Server.CreateRequestContextAsync(context, token).ConfigureAwait(false);
                await _Server.HandleClientTelemetryHistorySearchAsync(context, requestContext, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private async Task ProvidersHistorySearchAsync(HttpContextBase context)
        {
            await _Server.ExecuteRouteAsync(context, async token =>
            {
                RequestContext requestContext = await _Server.CreateRequestContextAsync(context, token).ConfigureAwait(false);
                await _Server.HandleProviderTelemetryHistorySearchAsync(context, requestContext, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private async Task HistoryRollupsAsync(HttpContextBase context)
        {
            await _Server.ExecuteRouteAsync(context, async token =>
            {
                RequestContext requestContext = await _Server.CreateRequestContextAsync(context, token).ConfigureAwait(false);
                await _Server.HandleClientTelemetryHistoryRollupsAsync(context, requestContext, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private async Task ConnectedHistoryAsync(HttpContextBase context)
        {
            await _Server.ExecuteRouteAsync(context, async token =>
            {
                RequestContext requestContext = await _Server.CreateRequestContextAsync(context, token).ConfigureAwait(false);
                await _Server.HandleConnectedClientHistoryAsync(context, requestContext, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private async Task ProvidersHealthAsync(HttpContextBase context)
        {
            await _Server.ExecuteRouteAsync(context, async token =>
            {
                RequestContext requestContext = await _Server.CreateRequestContextAsync(context, token).ConfigureAwait(false);
                await _Server.HandleProviderHealthAsync(context, requestContext, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private async Task ProvidersHistoryRollupsAsync(HttpContextBase context)
        {
            await _Server.ExecuteRouteAsync(context, async token =>
            {
                RequestContext requestContext = await _Server.CreateRequestContextAsync(context, token).ConfigureAwait(false);
                await _Server.HandleProviderTelemetryHistoryRollupsAsync(context, requestContext, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private async Task CollectionAttemptsAsync(HttpContextBase context)
        {
            await _Server.ExecuteRouteAsync(context, async token =>
            {
                RequestContext requestContext = await _Server.CreateRequestContextAsync(context, token).ConfigureAwait(false);
                await _Server.HandleClientTelemetryCollectionAttemptsAsync(context, requestContext, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private async Task MetricsAsync(HttpContextBase context)
        {
            await _Server.ExecuteRouteAsync(context, async token =>
            {
                RequestContext requestContext = await _Server.CreateRequestContextAsync(context, token).ConfigureAwait(false);
                await _Server.HandleClientTelemetryMetricsAsync(context, requestContext, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private async Task HistoryGetAsync(HttpContextBase context)
        {
            await _Server.ExecuteRouteAsync(context, async token =>
            {
                RequestContext requestContext = await _Server.CreateRequestContextAsync(context, token).ConfigureAwait(false);
                await _Server.HandleClientTelemetryHistoryGetAsync(context, requestContext, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private async Task HistoryDeleteAsync(HttpContextBase context)
        {
            await _Server.ExecuteRouteAsync(context, async token =>
            {
                RequestContext requestContext = await _Server.CreateRequestContextAsync(context, token).ConfigureAwait(false);
                await _Server.HandleClientTelemetryHistoryDeleteAsync(context, requestContext, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private async Task ProvidersHistoryGetAsync(HttpContextBase context)
        {
            await _Server.ExecuteRouteAsync(context, async token =>
            {
                RequestContext requestContext = await _Server.CreateRequestContextAsync(context, token).ConfigureAwait(false);
                await _Server.HandleProviderTelemetryHistoryGetAsync(context, requestContext, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private async Task ProvidersHistoryDeleteAsync(HttpContextBase context)
        {
            await _Server.ExecuteRouteAsync(context, async token =>
            {
                RequestContext requestContext = await _Server.CreateRequestContextAsync(context, token).ConfigureAwait(false);
                await _Server.HandleProviderTelemetryHistoryDeleteAsync(context, requestContext, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private async Task HistoryBulkDeleteAsync(HttpContextBase context)
        {
            await _Server.ExecuteRouteAsync(context, async token =>
            {
                RequestContext requestContext = await _Server.CreateRequestContextAsync(context, token).ConfigureAwait(false);
                await _Server.HandleClientTelemetryHistoryBulkDeleteAsync(context, requestContext, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private async Task ProvidersHistoryBulkDeleteAsync(HttpContextBase context)
        {
            await _Server.ExecuteRouteAsync(context, async token =>
            {
                RequestContext requestContext = await _Server.CreateRequestContextAsync(context, token).ConfigureAwait(false);
                await _Server.HandleProviderTelemetryHistoryBulkDeleteAsync(context, requestContext, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        #endregion
    }
}

namespace Xeno.Server.Routes
{
    using System;
    using System.Globalization;
    using System.Net;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;
    using Xeno.Core;
    using Xeno.Core.Serialization;

    /// <summary>
    /// Route registrar for the unauthenticated health and ping endpoints. These are fully self-contained
    /// (they depend only on <see cref="Constants"/>), so they own both their Watson route registrations
    /// and their handlers instead of being dispatched through the monolithic default route handler.
    /// </summary>
    public class HealthRoutes
    {
        #region Public-Methods

        /// <summary>
        /// Register the health and ping routes on the supplied web server.
        /// </summary>
        /// <param name="server">Web server.</param>
        /// <param name="metadataFactory">Factory that builds OpenAPI metadata for a route, matching the rest of the surface.</param>
        /// <param name="exceptionHandler">Shared route exception handler.</param>
        public void Register(
            Webserver server,
            Func<HttpMethod, string, string, string, OpenApiRouteMetadata> metadataFactory,
            Func<HttpContextBase, Exception, Task> exceptionHandler)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            if (metadataFactory == null) throw new ArgumentNullException(nameof(metadataFactory));
            if (exceptionHandler == null) throw new ArgumentNullException(nameof(exceptionHandler));

            AddHealth(server, "/", metadataFactory, exceptionHandler);
            AddHealth(server, "/health", metadataFactory, exceptionHandler);
            AddHealth(server, "/v1.0/api/health", metadataFactory, exceptionHandler);

            AddPing(server, "/ping", metadataFactory, exceptionHandler);
            AddPing(server, "/api/v1/ping", metadataFactory, exceptionHandler);
            AddPing(server, "/v1.0/api/ping", metadataFactory, exceptionHandler);
        }

        #endregion

        #region Private-Methods

        private void AddHealth(Webserver server, string path, Func<HttpMethod, string, string, string, OpenApiRouteMetadata> metadataFactory, Func<HttpContextBase, Exception, Task> exceptionHandler)
        {
            server.Routes.PreAuthentication.Static.Add(
                HttpMethod.GET,
                path,
                HealthAsync,
                exceptionHandler,
                openApiMetadata: metadataFactory(HttpMethod.GET, path, "Health check", "Health"));
        }

        private void AddPing(Webserver server, string path, Func<HttpMethod, string, string, string, OpenApiRouteMetadata> metadataFactory, Func<HttpContextBase, Exception, Task> exceptionHandler)
        {
            server.Routes.PreAuthentication.Static.Add(
                HttpMethod.GET,
                path,
                PingAsync,
                exceptionHandler,
                openApiMetadata: metadataFactory(HttpMethod.GET, path, "Ping heartbeat", "Health"));
        }

        private async Task HealthAsync(HttpContextBase context)
        {
            context.Response.ContentType = "application/json";
            await context.Response.Send(JsonUtil.Serialize(new
            {
                status = "healthy",
                product = Constants.ProductName,
                version = Constants.Version
            }, true), context.Token).ConfigureAwait(false);
        }

        private async Task PingAsync(HttpContextBase context)
        {
            string sequenceValue = DecodeQueryValue(context.Request.Query.Elements.Get("sequence"));
            string clientSentUtc = DecodeQueryValue(context.Request.Query.Elements.Get("clientSentUtc"));
            string? echoedClientSentUtc = String.IsNullOrWhiteSpace(clientSentUtc) ? null : clientSentUtc;
            int sequence = 0;
            Int32.TryParse(sequenceValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out sequence);

            context.Response.ContentType = "application/json";
            await context.Response.Send(JsonUtil.Serialize(new
            {
                status = "healthy",
                product = Constants.ProductName,
                version = Constants.Version,
                sequence,
                clientSentUtc = echoedClientSentUtc,
                serverUtc = DateTime.UtcNow
            }, true), context.Token).ConfigureAwait(false);
        }

        private static string DecodeQueryValue(string? value)
        {
            if (String.IsNullOrWhiteSpace(value)) return String.Empty;
            return WebUtility.UrlDecode(value) ?? value;
        }

        #endregion
    }
}

namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// HTTP handler that returns canned responses by path and records the requests it receives.
    /// </summary>
    public sealed class StubHttpHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, KeyValuePair<HttpStatusCode, string>> _Responses = new Dictionary<string, KeyValuePair<HttpStatusCode, string>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Requests received, in order.
        /// </summary>
        public List<HttpRequestMessage> Requests { get; } = new List<HttpRequestMessage>();

        /// <summary>
        /// Register a response for a path.
        /// </summary>
        /// <param name="path">Absolute path, for example /v1/models.</param>
        /// <param name="status">Status code.</param>
        /// <param name="json">JSON body.</param>
        /// <returns>This handler.</returns>
        public StubHttpHandler On(string path, HttpStatusCode status, string json)
        {
            _Responses[path] = new KeyValuePair<HttpStatusCode, string>(status, json);
            return this;
        }

        /// <summary>
        /// Return the canned response, or 404.
        /// </summary>
        /// <param name="request">Request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Response.</returns>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            string path = request.RequestUri?.AbsolutePath ?? String.Empty;
            if (!_Responses.TryGetValue(path, out KeyValuePair<HttpStatusCode, string> response))
            {
                response = new KeyValuePair<HttpStatusCode, string>(HttpStatusCode.NotFound, "{\"message\":\"not found\"}");
            }

            return Task.FromResult(new HttpResponseMessage(response.Key)
            {
                Content = new StringContent(response.Value, Encoding.UTF8, "application/json"),
                ReasonPhrase = response.Key.ToString()
            });
        }
    }
}

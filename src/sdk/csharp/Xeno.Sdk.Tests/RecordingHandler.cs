namespace Xeno.Sdk.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _Responder;

        public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _Responder = responder ?? throw new ArgumentNullException(nameof(responder));
        }

        public List<RecordedRequest> Requests { get; } = new List<RecordedRequest>();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string body = request.Content == null
                ? String.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            request.Headers.TryGetValues("x-tenant-guid", out IEnumerable<string>? tenantValues);
            Requests.Add(new RecordedRequest
            {
                Method = request.Method,
                Uri = request.RequestUri,
                Body = body,
                AuthorizationScheme = request.Headers.Authorization?.Scheme,
                AuthorizationParameter = request.Headers.Authorization?.Parameter,
                TenantId = tenantValues == null ? null : String.Join(",", tenantValues),
                CancellationToken = cancellationToken
            });

            return _Responder(request);
        }
    }
}

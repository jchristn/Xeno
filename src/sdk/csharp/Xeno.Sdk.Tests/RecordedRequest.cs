namespace Xeno.Sdk.Tests
{
    using System;
    using System.Net.Http;
    using System.Threading;

    internal sealed class RecordedRequest
    {
        public HttpMethod Method { get; set; } = HttpMethod.Get;

        public Uri? Uri { get; set; }

        public string Body { get; set; } = String.Empty;

        public string? AuthorizationScheme { get; set; }

        public string? AuthorizationParameter { get; set; }

        public string? AccessKey { get; set; }

        public string? TenantId { get; set; }

        public CancellationToken CancellationToken { get; set; }
    }
}

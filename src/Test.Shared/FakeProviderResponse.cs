namespace Test.Shared
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// How <see cref="FakeProviderServer"/> answers one request: a plain body, or chunks written one at a time.
    /// </summary>
    internal sealed class FakeProviderResponse
    {
        internal int StatusCode { get; set; } = 200;

        internal string ContentType { get; set; } = "application/json";

        internal string Body { get; set; } = "{}";

        internal List<string> Chunks { get; set; } = new List<string>();

        internal int ChunkDelayMs { get; set; } = 0;

        internal int DelayMs { get; set; } = 0;

        internal int? AbortAfterChunks { get; set; } = null;

        internal Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        internal static FakeProviderResponse Json(string body, int statusCode = 200)
        {
            return new FakeProviderResponse { Body = body, StatusCode = statusCode };
        }
    }
}

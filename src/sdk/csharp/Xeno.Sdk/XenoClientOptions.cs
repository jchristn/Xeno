namespace Xeno.Sdk
{
    using System.Net.Http;

    /// <summary>
    /// Configuration for <see cref="XenoClient"/>.
    /// </summary>
    public sealed class XenoClientOptions
    {
        /// <summary>
        /// Gets or sets the Xeno server base URL. Default value is <c>http://localhost:9000</c>.
        /// </summary>
        public string BaseUrl { get; set; } = "http://localhost:9000";

        /// <summary>
        /// Gets or sets an optional bearer token for authenticated REST calls.
        /// </summary>
        public string? BearerToken { get; set; }

        /// <summary>
        /// Gets or sets an optional tenant-bound credential access key.
        /// </summary>
        public string? AccessKey { get; set; }

        /// <summary>
        /// Gets or sets an optional tenant-bound credential secret key.
        /// </summary>
        public string? SecretKey { get; set; }

        /// <summary>
        /// Gets or sets the tenant identifier sent as <c>x-tenant-guid</c>. Default value is <c>ten_default</c>.
        /// </summary>
        public string TenantId { get; set; } = "ten_default";

        /// <summary>
        /// Gets or sets an optional caller-owned HTTP client. When supplied, the SDK does not dispose it.
        /// </summary>
        public HttpClient? HttpClient { get; set; }
    }
}

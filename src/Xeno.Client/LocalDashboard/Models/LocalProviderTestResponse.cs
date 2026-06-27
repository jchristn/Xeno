namespace Xeno.Client.LocalDashboard.Models
{
    /// <summary>
    /// Local provider connectivity test response.
    /// </summary>
    public class LocalProviderTestResponse
    {
        /// <summary>
        /// Provider name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Endpoint tested.
        /// </summary>
        public string Endpoint { get; set; } = string.Empty;

        /// <summary>
        /// Whether the endpoint was reachable.
        /// </summary>
        public bool Success { get; set; } = false;

        /// <summary>
        /// HTTP status code when available.
        /// </summary>
        public int? StatusCode { get; set; } = null;

        /// <summary>
        /// Error message when the test fails.
        /// </summary>
        public string? Error { get; set; } = null;
    }
}

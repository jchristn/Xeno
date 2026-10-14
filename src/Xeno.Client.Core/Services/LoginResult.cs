namespace Xeno.Client.Services
{
    /// <summary>
    /// Result of signing in to a Xeno server.
    /// </summary>
    public class LoginResult
    {
        /// <summary>
        /// True when the server issued a client credential and it was stored.
        /// </summary>
        public bool Success { get; set; } = false;

        /// <summary>
        /// Tenant the credential is bound to.
        /// </summary>
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// HTTP status code returned by the server, or null when the request did not complete.
        /// </summary>
        public int? StatusCode { get; set; } = null;

        /// <summary>
        /// Failure description, or null on success.
        /// </summary>
        public string? Error { get; set; } = null;
    }
}

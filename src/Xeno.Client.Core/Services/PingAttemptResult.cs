namespace Xeno.Client.Services
{
    using System;

    /// <summary>
    /// Result of one HTTP heartbeat sent to the Xeno server.
    /// </summary>
    public class PingAttemptResult
    {
        /// <summary>
        /// Sequence number, starting at 1.
        /// </summary>
        public int Sequence { get; set; } = 1;

        /// <summary>
        /// True when the server answered with a 2xx status.
        /// </summary>
        public bool Success { get; set; } = false;

        /// <summary>
        /// HTTP status code, or 0 when the request failed or timed out.
        /// </summary>
        public int StatusCode { get; set; } = 0;

        /// <summary>
        /// HTTP reason phrase, when available.
        /// </summary>
        public string? ReasonPhrase { get; set; } = null;

        /// <summary>
        /// Response size in bytes.
        /// </summary>
        public int Bytes { get; set; } = 0;

        /// <summary>
        /// Round-trip time in milliseconds.
        /// </summary>
        public long ElapsedMs { get; set; } = 0;

        /// <summary>
        /// Status reported by the server.
        /// </summary>
        public string Status { get; set; } = String.Empty;

        /// <summary>
        /// Server time reported by the server.
        /// </summary>
        public string ServerUtc { get; set; } = String.Empty;

        /// <summary>
        /// Failure description (for example "Timeout"), or null.
        /// </summary>
        public string? Error { get; set; } = null;
    }
}

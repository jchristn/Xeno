namespace Xeno.Core.Exceptions
{
    using System;

    /// <summary>
    /// Exception raised when a request is authenticated but not authorized.
    /// </summary>
    public class AuthorizationDeniedException : UnauthorizedAccessException
    {
        /// <summary>
        /// Instantiate the exception.
        /// </summary>
        /// <param name="statusCode">HTTP status code to return.</param>
        /// <param name="reasonCode">Machine-readable reason code.</param>
        /// <param name="message">Human-readable message.</param>
        public AuthorizationDeniedException(int statusCode, string reasonCode, string message)
            : base(message)
        {
            StatusCode = statusCode;
            ReasonCode = reasonCode;
        }

        /// <summary>
        /// HTTP status code to return.
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// Machine-readable reason code.
        /// </summary>
        public string ReasonCode { get; set; }
    }
}

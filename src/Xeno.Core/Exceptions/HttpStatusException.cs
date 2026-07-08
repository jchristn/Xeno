namespace Xeno.Core.Exceptions
{
    using System;

    /// <summary>
    /// Exception raised when a REST handler needs to return a specific HTTP status code.
    /// </summary>
    public class HttpStatusException : Exception
    {
        /// <summary>
        /// Instantiate the exception.
        /// </summary>
        /// <param name="statusCode">HTTP status code to return.</param>
        /// <param name="reasonCode">Machine-readable reason code.</param>
        /// <param name="message">Human-readable message.</param>
        public HttpStatusException(int statusCode, string reasonCode, string message)
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

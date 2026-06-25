namespace Xeno.Sdk
{
    using System;
    using System.Net;

    /// <summary>
    /// Exception thrown when the Xeno API returns a non-success status code.
    /// </summary>
    public sealed class XenoApiException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="XenoApiException"/> class.
        /// </summary>
        /// <param name="message">Error message.</param>
        /// <param name="statusCode">HTTP status code.</param>
        /// <param name="endpoint">Requested endpoint.</param>
        /// <param name="responseBody">Raw response body.</param>
        public XenoApiException(string message, HttpStatusCode statusCode, string endpoint, string responseBody) : base(message)
        {
            StatusCode = statusCode;
            Endpoint = endpoint;
            ResponseBody = responseBody;
        }

        /// <summary>
        /// Gets the HTTP status code.
        /// </summary>
        public HttpStatusCode StatusCode { get; }

        /// <summary>
        /// Gets the requested endpoint.
        /// </summary>
        public string Endpoint { get; }

        /// <summary>
        /// Gets the raw response body.
        /// </summary>
        public string ResponseBody { get; }
    }
}

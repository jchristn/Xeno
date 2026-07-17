namespace Xeno.Client.LocalInference
{
    using System;
    using Xeno.Client.LocalInference.Models;

    /// <summary>
    /// Creates provider-compatible local inference error responses.
    /// </summary>
    public static class LocalInferenceErrorFactory
    {
        /// <summary>
        /// Build an OpenAI-compatible error response.
        /// </summary>
        /// <param name="message">Error message.</param>
        /// <param name="type">OpenAI error type.</param>
        /// <param name="param">Parameter name.</param>
        /// <param name="code">Error code.</param>
        /// <returns>Error response.</returns>
        public static OpenAiErrorResponse OpenAi(string message, string type, string? param = null, string? code = null)
        {
            return new OpenAiErrorResponse
            {
                Error = new OpenAiErrorDetail
                {
                    Message = String.IsNullOrWhiteSpace(message) ? "Request failed." : message,
                    Type = String.IsNullOrWhiteSpace(type) ? "server_error" : type,
                    Param = param,
                    Code = code
                }
            };
        }

        /// <summary>
        /// Build an Ollama-compatible error response.
        /// </summary>
        /// <param name="message">Error message.</param>
        /// <returns>Error response.</returns>
        public static OllamaErrorResponse Ollama(string message)
        {
            return new OllamaErrorResponse { Error = String.IsNullOrWhiteSpace(message) ? "request failed" : message };
        }

        /// <summary>
        /// Build a small generic error response for unknown API families.
        /// </summary>
        /// <param name="message">Error message.</param>
        /// <returns>Error response.</returns>
        public static LocalInferenceGenericErrorResponse Generic(string message)
        {
            return new LocalInferenceGenericErrorResponse
            {
                Error = "NotFound",
                Message = String.IsNullOrWhiteSpace(message) ? "Route not found." : message
            };
        }
    }
}

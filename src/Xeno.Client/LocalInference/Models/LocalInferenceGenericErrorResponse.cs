namespace Xeno.Client.LocalInference.Models
{
    /// <summary>
    /// Generic local inference error response for unknown API families.
    /// </summary>
    public class LocalInferenceGenericErrorResponse
    {
        /// <summary>
        /// Error code.
        /// </summary>
        public string Error { get; set; } = "Error";

        /// <summary>
        /// Error message.
        /// </summary>
        public string Message { get; set; } = "Request failed.";
    }
}

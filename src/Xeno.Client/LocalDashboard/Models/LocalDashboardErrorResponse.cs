namespace Xeno.Client.LocalDashboard.Models
{
    /// <summary>
    /// Local dashboard API error response.
    /// </summary>
    public class LocalDashboardErrorResponse
    {
        /// <summary>
        /// Error code.
        /// </summary>
        public string Error { get; set; } = "Error";

        /// <summary>
        /// Error message.
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }
}

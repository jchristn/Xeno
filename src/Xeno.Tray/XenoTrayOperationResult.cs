namespace Xeno.Tray
{
    /// <summary>
    /// Result from a tray operation.
    /// </summary>
    public sealed class XenoTrayOperationResult
    {
        /// <summary>
        /// Whether the operation succeeded.
        /// </summary>
        public bool Succeeded { get; set; } = true;

        /// <summary>
        /// Human-readable result message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Create a successful result.
        /// </summary>
        /// <param name="message">Message.</param>
        /// <returns>Operation result.</returns>
        public static XenoTrayOperationResult Success(string message)
        {
            return new XenoTrayOperationResult { Succeeded = true, Message = message };
        }

        /// <summary>
        /// Create a failed result.
        /// </summary>
        /// <param name="message">Message.</param>
        /// <returns>Operation result.</returns>
        public static XenoTrayOperationResult Failure(string message)
        {
            return new XenoTrayOperationResult { Succeeded = false, Message = message };
        }
    }
}

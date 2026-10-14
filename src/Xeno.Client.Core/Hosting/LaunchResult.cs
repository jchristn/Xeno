namespace Xeno.Client.Hosting
{
    using System;

    /// <summary>
    /// Outcome of launching an application, terminal window, URL, or folder.
    /// </summary>
    public class LaunchResult
    {
        /// <summary>
        /// True when the launch was started.
        /// </summary>
        public bool Success { get; set; } = false;

        /// <summary>
        /// Human-readable description of what happened or why it failed.
        /// </summary>
        public string Message { get; set; } = String.Empty;

        /// <summary>
        /// Create a successful result.
        /// </summary>
        /// <param name="message">Message.</param>
        /// <returns>Result.</returns>
        public static LaunchResult Ok(string message)
        {
            return new LaunchResult { Success = true, Message = message };
        }

        /// <summary>
        /// Create a failed result.
        /// </summary>
        /// <param name="message">Message.</param>
        /// <returns>Result.</returns>
        public static LaunchResult Fail(string message)
        {
            return new LaunchResult { Success = false, Message = message };
        }
    }
}

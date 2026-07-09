namespace Xeno.Email
{
    using System;

    /// <summary>
    /// Exception thrown when email delivery fails.
    /// </summary>
    public class EmailDeliveryException : Exception
    {
        /// <summary>
        /// Instantiate the exception.
        /// </summary>
        /// <param name="message">Message.</param>
        public EmailDeliveryException(string message) : base(message)
        {
        }

        /// <summary>
        /// Instantiate the exception.
        /// </summary>
        /// <param name="message">Message.</param>
        /// <param name="innerException">Inner exception.</param>
        public EmailDeliveryException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}

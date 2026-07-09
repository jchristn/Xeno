namespace Xeno.Core.Settings
{
    using System;

    /// <summary>
    /// Email recipient settings.
    /// </summary>
    public class EmailRecipientSettings
    {
        /// <summary>
        /// Recipient email address.
        /// </summary>
        public string Email { get; set; } = String.Empty;

        /// <summary>
        /// Optional recipient display name.
        /// </summary>
        public string Name { get; set; } = String.Empty;
    }
}

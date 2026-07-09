namespace Xeno.Email
{
    using System;
    using System.Net.Mail;

    /// <summary>
    /// Email address with an optional display name.
    /// </summary>
    public class EmailAddress
    {
        /// <summary>
        /// Email address.
        /// </summary>
        public string Address { get; set; } = String.Empty;

        /// <summary>
        /// Optional display name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Create an email address.
        /// </summary>
        /// <param name="address">Email address.</param>
        /// <param name="name">Optional display name.</param>
        /// <returns>Email address.</returns>
        public static EmailAddress Create(string address, string? name = null)
        {
            if (String.IsNullOrWhiteSpace(address)) throw new ArgumentException("Email address is required.", nameof(address));
            return new EmailAddress
            {
                Address = address.Trim(),
                Name = String.IsNullOrWhiteSpace(name) ? String.Empty : name.Trim()
            };
        }

        /// <summary>
        /// Convert to a mail address.
        /// </summary>
        /// <returns>Mail address.</returns>
        public MailAddress ToMailAddress()
        {
            if (String.IsNullOrWhiteSpace(Address)) throw new InvalidOperationException("Email address is required.");
            return String.IsNullOrWhiteSpace(Name) ? new MailAddress(Address) : new MailAddress(Address, Name);
        }

        /// <summary>
        /// Convert to a display-safe provider string.
        /// </summary>
        /// <returns>Email address string.</returns>
        public override string ToString()
        {
            if (String.IsNullOrWhiteSpace(Name)) return Address;
            return Name + " <" + Address + ">";
        }
    }
}

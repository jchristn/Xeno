namespace Xeno.Email.Templates
{
    /// <summary>
    /// Context for the tenant approval email template.
    /// </summary>
    public class TenantApprovedEmailContext
    {
        /// <summary>
        /// Organization (tenant) name.
        /// </summary>
        public string Organization { get; set; } = string.Empty;

        /// <summary>
        /// Contact person's name.
        /// </summary>
        public string ContactName { get; set; } = string.Empty;

        /// <summary>
        /// Sign-in email address.
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Absolute sign-in URL.
        /// </summary>
        public string SignInUrl { get; set; } = string.Empty;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public TenantApprovedEmailContext()
        {
        }
    }
}

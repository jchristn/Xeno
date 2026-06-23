namespace Xeno.Core.Settings
{
    using System;
    using System.Collections.Generic;
    using Xeno.Core.Enums;

    /// <summary>
    /// Authentication settings.
    /// </summary>
    public class AuthSettings
    {
        /// <summary>
        /// Token issuer.
        /// </summary>
        public string Issuer { get; set; } = "xeno";

        /// <summary>
        /// Signing key.
        /// </summary>
        public string SigningKey { get; set; } = "development-only-change-me";

        /// <summary>
        /// Admin API key.
        /// </summary>
        public string AdminApiKey { get; set; } = "xenoadmin";
    }
}

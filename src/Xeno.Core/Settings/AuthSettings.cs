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
        /// Session lifetime in minutes.
        /// </summary>
        public int SessionMinutes { get; set; } = 60;

        /// <summary>
        /// Maximum login or token-creation attempts per source IP per minute.
        /// </summary>
        public int MaxLoginRequestsPerMinute { get; set; } = 20;
    }
}

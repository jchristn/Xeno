namespace Xeno.Core.Settings
{
    using System;
    using Xeno.Core.Enums;

    /// <summary>
    /// Captcha settings. Field names are provider-agnostic: both Cloudflare Turnstile and Google
    /// reCAPTCHA use a public site key rendered by the web app and a secret key used for server-side
    /// verification.
    /// </summary>
    public class CaptchaSettings
    {
        /// <summary>
        /// Captcha provider. Defaults to <see cref="CaptchaProviderEnum.None"/>.
        /// </summary>
        public CaptchaProviderEnum Provider { get; set; } = CaptchaProviderEnum.None;

        /// <summary>
        /// Public site key rendered by the web app captcha widget.
        /// </summary>
        public string? SiteKey { get; set; } = null;

        /// <summary>
        /// Secret key used for server-side verification. Sensitive; redact when logging or serializing
        /// for display.
        /// </summary>
        public string? SecretKey { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public CaptchaSettings()
        {
        }
    }
}

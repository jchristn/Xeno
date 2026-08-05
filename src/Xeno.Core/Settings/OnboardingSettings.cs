namespace Xeno.Core.Settings
{
    using System;
    using Xeno.Core.Enums;

    /// <summary>
    /// Public onboarding (sign-up) settings.
    /// </summary>
    public class OnboardingSettings
    {
        /// <summary>
        /// Onboarding mode. Defaults to <see cref="OnboardingModeEnum.Open"/> so self-hosted
        /// deployments keep immediate self-service sign-up; cloud deployments set
        /// <see cref="OnboardingModeEnum.VerifyAndApprove"/>.
        /// </summary>
        public OnboardingModeEnum Mode { get; set; } = OnboardingModeEnum.Open;

        /// <summary>
        /// Base URL used to build verification and sign-in links in onboarding email. When empty,
        /// the email application base URL is used.
        /// </summary>
        public string? LinkBaseUrl { get; set; } = null;

        /// <summary>
        /// Cloudflare Turnstile secret key for server-side captcha verification. When empty, captcha
        /// is enforced by the web app only and the server does not verify the token.
        /// </summary>
        public string? CaptchaSecretKey { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public OnboardingSettings()
        {
        }
    }
}

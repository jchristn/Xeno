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
        /// Captcha settings for public sign-up. When the provider is None, captcha is not enforced.
        /// </summary>
        public CaptchaSettings Captcha
        {
            get => _Captcha;
            set => _Captcha = value ?? new CaptchaSettings();
        }

        private CaptchaSettings _Captcha = new CaptchaSettings();

        /// <summary>
        /// Instantiate.
        /// </summary>
        public OnboardingSettings()
        {
        }
    }
}

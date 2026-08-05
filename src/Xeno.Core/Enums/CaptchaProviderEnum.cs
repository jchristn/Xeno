namespace Xeno.Core.Enums
{
    /// <summary>
    /// Captcha provider used to protect public onboarding sign-up.
    /// </summary>
    public enum CaptchaProviderEnum
    {
        /// <summary>
        /// No captcha is enforced.
        /// </summary>
        None,

        /// <summary>
        /// Cloudflare Turnstile.
        /// </summary>
        CloudflareTurnstile,

        /// <summary>
        /// Google reCAPTCHA.
        /// </summary>
        GoogleReCaptcha
    }
}

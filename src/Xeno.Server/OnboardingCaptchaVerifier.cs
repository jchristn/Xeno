namespace Xeno.Server
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Enums;
    using Xeno.Core.Settings;

    /// <summary>
    /// Verifies onboarding captcha tokens against the configured provider. Cloudflare Turnstile and
    /// Google reCAPTCHA share the same verification request shape (secret, response, remoteip) and a
    /// JSON response with a boolean <c>success</c>, so a single verifier serves both.
    /// </summary>
    internal static class OnboardingCaptchaVerifier
    {
        private static readonly HttpClient _Http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        /// <summary>
        /// Verify a captcha token. Returns true when the provider is None, when no server-side secret
        /// is configured (verification is left to the web app), or when the provider confirms the
        /// token. Returns false when a configured provider rejects or cannot verify the token.
        /// </summary>
        /// <param name="settings">Captcha settings.</param>
        /// <param name="captchaToken">Token from the web app widget.</param>
        /// <param name="remoteIp">Caller IP, when known.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when the request may proceed.</returns>
        public static async Task<bool> VerifyAsync(CaptchaSettings settings, string? captchaToken, string? remoteIp, CancellationToken token)
        {
            if (settings == null || settings.Provider == CaptchaProviderEnum.None) return true;
            if (String.IsNullOrWhiteSpace(settings.SecretKey)) return true;
            if (String.IsNullOrWhiteSpace(captchaToken)) return false;

            string url = settings.Provider == CaptchaProviderEnum.CloudflareTurnstile
                ? "https://challenges.cloudflare.com/turnstile/v0/siteverify"
                : "https://www.google.com/recaptcha/api/siteverify";

            List<KeyValuePair<string, string>> form = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("secret", settings.SecretKey),
                new KeyValuePair<string, string>("response", captchaToken)
            };
            if (!String.IsNullOrWhiteSpace(remoteIp)) form.Add(new KeyValuePair<string, string>("remoteip", remoteIp));

            try
            {
                using (FormUrlEncodedContent content = new FormUrlEncodedContent(form))
                using (HttpResponseMessage response = await _Http.PostAsync(url, content, token).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode) return false;
                    string body = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                    using (JsonDocument document = JsonDocument.Parse(body))
                    {
                        return document.RootElement.TryGetProperty("success", out JsonElement success) && success.ValueKind == JsonValueKind.True;
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}

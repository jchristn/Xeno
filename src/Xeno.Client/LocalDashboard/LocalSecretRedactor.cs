namespace Xeno.Client.LocalDashboard
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Redacts local dashboard secrets.
    /// </summary>
    public static class LocalSecretRedactor
    {
        /// <summary>
        /// Redact headers with sensitive names.
        /// </summary>
        /// <param name="headers">Headers.</param>
        /// <returns>Redacted headers.</returns>
        public static Dictionary<string, string> RedactHeaders(Dictionary<string, string>? headers)
        {
            Dictionary<string, string> redacted = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (headers == null) return redacted;
            foreach (KeyValuePair<string, string> header in headers)
            {
                string name = CanonicalizeHeaderName(header.Key);
                if (String.IsNullOrWhiteSpace(name)) continue;
                redacted[name] = IsSensitive(name) ? "redacted" : header.Value;
            }

            return redacted;
        }

        /// <summary>
        /// Redact a provider object.
        /// </summary>
        /// <param name="provider">Provider.</param>
        /// <returns>Redacted provider.</returns>
        public static ClientRunnerSettings RedactProvider(ClientRunnerSettings provider)
        {
            ClientRunnerSettings copy = new ClientRunnerSettings
            {
                Name = provider.Name,
                ApiType = provider.ApiType,
                Endpoint = provider.Endpoint,
                Credential = String.IsNullOrWhiteSpace(provider.Credential) ? null : "configured",
                TimeoutMs = provider.TimeoutMs,
                Models = provider.Models
            };
            return copy;
        }

        private static bool IsSensitive(string name)
        {
            if (String.IsNullOrWhiteSpace(name)) return false;
            string normalized = name.ToLowerInvariant();
            return normalized.Contains("authorization", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("api-key", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("x-api-key", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("secret", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("token", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("cookie", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("credential", StringComparison.OrdinalIgnoreCase);
        }

        private static string CanonicalizeHeaderName(string name)
        {
            if (String.IsNullOrWhiteSpace(name)) return String.Empty;
            if (String.Equals(name, "x-forwarded-for", StringComparison.OrdinalIgnoreCase)) return "X-Forwarded-For";
            if (String.Equals(name, "x-real-ip", StringComparison.OrdinalIgnoreCase)) return "X-Real-IP";
            if (String.Equals(name, "x-xeno-request-id", StringComparison.OrdinalIgnoreCase)) return "X-Xeno-Request-ID";
            if (String.Equals(name, "x-xeno-requestor-ip", StringComparison.OrdinalIgnoreCase)) return "X-Xeno-Requestor-IP";
            if (String.Equals(name, "x-xeno-tenant-id", StringComparison.OrdinalIgnoreCase)) return "X-Xeno-Tenant-ID";
            if (String.Equals(name, "content-type", StringComparison.OrdinalIgnoreCase)) return "Content-Type";
            if (String.Equals(name, "authorization", StringComparison.OrdinalIgnoreCase)) return "Authorization";
            return name.Trim();
        }
    }
}

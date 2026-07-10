namespace Xeno.Email.Templates
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using Xeno.Core.Helpers;

    /// <summary>
    /// Email redaction helpers.
    /// </summary>
    public static class EmailRedactionHelpers
    {
        /// <summary>
        /// Redacted value placeholder.
        /// </summary>
        public const string RedactedValue = "[redacted]";

        private static readonly HashSet<string> SensitiveJsonProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "password",
            "secret",
            "secretKey",
            "token",
            "authorization",
            "apiKey",
            "credential",
            "credentials",
            "accessKey",
            "x-token",
            "x-access-key"
        };

        private static readonly Regex SensitiveStringValueRegex = new Regex(
            "(\"(?:password|secret|secretKey|token|authorization|apiKey|credential|credentials|accessKey|x-token|x-access-key)\"\\s*:\\s*\")(?:\\\\.|[^\"\\\\])*(\"?)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        /// <summary>
        /// Redact sensitive JSON values.
        /// </summary>
        /// <param name="value">Value.</param>
        /// <returns>Redacted value.</returns>
        public static string RedactSensitiveJson(string value)
        {
            if (String.IsNullOrEmpty(value)) return value;

            try
            {
                return JsonRedactionHelper.RedactSensitiveJson(value, IsSensitiveJsonProperty, RedactedValue, true);
            }
            catch (JsonException)
            {
                return RedactSensitiveStringValues(value);
            }
            catch (NotSupportedException)
            {
                return RedactSensitiveStringValues(value);
            }
        }

        private static bool IsSensitiveJsonProperty(string? name)
        {
            return !String.IsNullOrWhiteSpace(name) && SensitiveJsonProperties.Contains(name);
        }

        private static string RedactSensitiveStringValues(string value)
        {
            return SensitiveStringValueRegex.Replace(
                value,
                match => match.Groups[1].Value + RedactedValue + (match.Groups[2].Success && match.Groups[2].Length > 0 ? match.Groups[2].Value : "\""));
        }
    }
}

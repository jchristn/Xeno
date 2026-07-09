namespace Xeno.Email.Templates
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Text.RegularExpressions;

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
                JsonNode? node = JsonNode.Parse(value);
                if (node == null) return RedactSensitiveStringValues(value);

                RedactSensitiveJsonNode(node);
                return node.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
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

        private static void RedactSensitiveJsonNode(JsonNode node)
        {
            if (node is JsonObject obj)
            {
                foreach (KeyValuePair<string, JsonNode?> property in obj.ToList())
                {
                    if (SensitiveJsonProperties.Contains(property.Key))
                    {
                        obj[property.Key] = RedactedValue;
                    }
                    else if (property.Value != null)
                    {
                        RedactSensitiveJsonNode(property.Value);
                    }
                }

                return;
            }

            if (node is JsonArray arr)
            {
                foreach (JsonNode? child in arr)
                {
                    if (child != null) RedactSensitiveJsonNode(child);
                }
            }
        }

        private static string RedactSensitiveStringValues(string value)
        {
            return SensitiveStringValueRegex.Replace(
                value,
                match => match.Groups[1].Value + RedactedValue + (match.Groups[2].Success && match.Groups[2].Length > 0 ? match.Groups[2].Value : "\""));
        }
    }
}

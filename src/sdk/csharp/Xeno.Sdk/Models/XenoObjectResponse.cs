namespace Xeno.Sdk
{
    using System;
    using System.Text.Json;

    /// <summary>
    /// JSON-backed response for endpoints whose contract is intentionally open ended.
    /// </summary>
    public class XenoObjectResponse
    {
        /// <summary>
        /// Gets or sets the response JSON value.
        /// </summary>
        public JsonElement Value { get; set; }

        /// <summary>
        /// Creates a response from raw JSON.
        /// </summary>
        /// <param name="json">Raw JSON payload.</param>
        /// <returns>JSON-backed response.</returns>
        /// <exception cref="JsonException">Thrown when <paramref name="json"/> is not valid JSON.</exception>
        public static XenoObjectResponse FromJson(string json)
        {
            using (JsonDocument document = JsonDocument.Parse(String.IsNullOrWhiteSpace(json) ? "{}" : json))
            {
                return new XenoObjectResponse
                {
                    Value = document.RootElement.Clone()
                };
            }
        }

        /// <summary>
        /// Returns the JSON payload.
        /// </summary>
        /// <returns>JSON payload.</returns>
        public override string ToString()
        {
            return Value.ValueKind == JsonValueKind.Undefined ? "{}" : Value.GetRawText();
        }
    }
}

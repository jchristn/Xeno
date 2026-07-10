namespace Xeno.Sdk
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Raw JSON value for intentionally open-ended response fields.
    /// </summary>
    [JsonConverter(typeof(RawJsonValueConverter))]
    public sealed class RawJsonValue
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RawJsonValue"/> class.
        /// </summary>
        public RawJsonValue()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RawJsonValue"/> class.
        /// </summary>
        /// <param name="json">Raw JSON value. Default value is <c>{}</c> when null or empty.</param>
        public RawJsonValue(string? json)
        {
            Json = String.IsNullOrWhiteSpace(json) ? "{}" : json;
        }

        /// <summary>
        /// Gets or sets the raw JSON value.
        /// </summary>
        public string Json { get; set; } = "{}";

        /// <summary>
        /// Returns the raw JSON value.
        /// </summary>
        /// <returns>Raw JSON value.</returns>
        public override string ToString()
        {
            return String.IsNullOrWhiteSpace(Json) ? "{}" : Json;
        }
    }
}

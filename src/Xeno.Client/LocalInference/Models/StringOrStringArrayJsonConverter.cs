namespace Xeno.Client.LocalInference.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// JSON converter for string-or-string-array provider request values.
    /// </summary>
    public class StringOrStringArrayJsonConverter : JsonConverter<StringOrStringArray>
    {
        /// <summary>
        /// Read a string or string array.
        /// </summary>
        /// <param name="reader">JSON reader.</param>
        /// <param name="typeToConvert">Type to convert.</param>
        /// <param name="options">Serializer options.</param>
        /// <returns>Converted value.</returns>
        public override StringOrStringArray Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            StringOrStringArray value = new StringOrStringArray();
            if (reader.TokenType == JsonTokenType.String)
            {
                value.Values.Add(reader.GetString() ?? String.Empty);
                value.IsArray = false;
                return value;
            }

            if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException("Expected a string or string array.");
            value.IsArray = true;
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray) return value;
                if (reader.TokenType != JsonTokenType.String) throw new JsonException("Array entries must be strings.");
                value.Values.Add(reader.GetString() ?? String.Empty);
            }

            throw new JsonException("Unexpected end of JSON array.");
        }

        /// <summary>
        /// Write a string or string array.
        /// </summary>
        /// <param name="writer">JSON writer.</param>
        /// <param name="value">Value.</param>
        /// <param name="options">Serializer options.</param>
        public override void Write(Utf8JsonWriter writer, StringOrStringArray value, JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(writer);
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            if (!value.IsArray && value.Values.Count <= 1)
            {
                writer.WriteStringValue(value.Values.Count == 0 ? String.Empty : value.Values[0]);
                return;
            }

            writer.WriteStartArray();
            foreach (string item in value.Values ?? new List<string>())
            {
                writer.WriteStringValue(item);
            }

            writer.WriteEndArray();
        }
    }
}

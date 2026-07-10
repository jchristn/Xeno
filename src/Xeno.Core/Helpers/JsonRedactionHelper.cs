namespace Xeno.Core.Helpers
{
    using System;
    using System.IO;
    using System.Text;
    using System.Text.Json;

    /// <summary>
    /// Streaming JSON redaction helpers that avoid materializing JSON DOM types.
    /// </summary>
    public static class JsonRedactionHelper
    {
        /// <summary>
        /// Redacts sensitive JSON property values from a JSON payload.
        /// </summary>
        /// <param name="value">JSON payload.</param>
        /// <param name="shouldRedactProperty">Predicate that returns true when a property value should be redacted.</param>
        /// <param name="redactedValue">Replacement value.</param>
        /// <param name="writeIndented">True to emit indented JSON.</param>
        /// <returns>Redacted JSON payload.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="shouldRedactProperty"/> is null.</exception>
        /// <exception cref="JsonException">Thrown when <paramref name="value"/> is not valid JSON.</exception>
        public static string RedactSensitiveJson(string value, Func<string?, bool> shouldRedactProperty, string redactedValue, bool writeIndented)
        {
            ArgumentNullException.ThrowIfNull(shouldRedactProperty);
            if (String.IsNullOrWhiteSpace(value)) return value;

            byte[] input = Encoding.UTF8.GetBytes(value);
            Utf8JsonReader reader = new Utf8JsonReader(input, new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            using (MemoryStream output = new MemoryStream())
            {
                using (Utf8JsonWriter writer = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = writeIndented }))
                {
                    if (!reader.Read()) return value;
                    WriteRedactedValue(ref reader, writer, null, shouldRedactProperty, redactedValue);
                }

                return Encoding.UTF8.GetString(output.ToArray());
            }
        }

        private static void WriteRedactedValue(ref Utf8JsonReader reader, Utf8JsonWriter writer, string? propertyName, Func<string?, bool> shouldRedactProperty, string redactedValue)
        {
            if (shouldRedactProperty(propertyName))
            {
                writer.WriteStringValue(redactedValue);
                SkipCurrentValue(ref reader);
                return;
            }

            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject:
                    writer.WriteStartObject();
                    while (reader.Read())
                    {
                        if (reader.TokenType == JsonTokenType.EndObject) break;
                        if (reader.TokenType != JsonTokenType.PropertyName) throw new JsonException("Expected JSON property name while redacting object.");

                        string childPropertyName = reader.GetString() ?? String.Empty;
                        writer.WritePropertyName(childPropertyName);
                        if (!reader.Read()) throw new JsonException("Expected JSON property value while redacting object.");
                        WriteRedactedValue(ref reader, writer, childPropertyName, shouldRedactProperty, redactedValue);
                    }

                    writer.WriteEndObject();
                    break;
                case JsonTokenType.StartArray:
                    writer.WriteStartArray();
                    while (reader.Read())
                    {
                        if (reader.TokenType == JsonTokenType.EndArray) break;
                        WriteRedactedValue(ref reader, writer, null, shouldRedactProperty, redactedValue);
                    }

                    writer.WriteEndArray();
                    break;
                case JsonTokenType.String:
                    writer.WriteStringValue(reader.GetString());
                    break;
                case JsonTokenType.Number:
                    WriteNumberValue(ref reader, writer);
                    break;
                case JsonTokenType.True:
                    writer.WriteBooleanValue(true);
                    break;
                case JsonTokenType.False:
                    writer.WriteBooleanValue(false);
                    break;
                case JsonTokenType.Null:
                    writer.WriteNullValue();
                    break;
                default:
                    throw new JsonException("Unsupported JSON token while redacting value: " + reader.TokenType + ".");
            }
        }

        private static void WriteNumberValue(ref Utf8JsonReader reader, Utf8JsonWriter writer)
        {
            if (reader.TryGetInt64(out long longValue))
            {
                writer.WriteNumberValue(longValue);
                return;
            }

            if (reader.TryGetDecimal(out decimal decimalValue))
            {
                writer.WriteNumberValue(decimalValue);
                return;
            }

            writer.WriteNumberValue(reader.GetDouble());
        }

        private static void SkipCurrentValue(ref Utf8JsonReader reader)
        {
            if (reader.TokenType == JsonTokenType.StartObject || reader.TokenType == JsonTokenType.StartArray)
            {
                if (!reader.TrySkip()) throw new JsonException("Unable to skip sensitive JSON value.");
            }
        }
    }
}

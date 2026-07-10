namespace Xeno.Sdk
{
    using System;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    internal sealed class RawJsonValueConverter : JsonConverter<RawJsonValue>
    {
        public override RawJsonValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using (MemoryStream output = new MemoryStream())
            {
                using (Utf8JsonWriter writer = new Utf8JsonWriter(output))
                {
                    WriteCurrentValue(ref reader, writer);
                }

                return new RawJsonValue(Encoding.UTF8.GetString(output.ToArray()));
            }
        }

        public override void Write(Utf8JsonWriter writer, RawJsonValue value, JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(writer);
            string json = value == null || String.IsNullOrWhiteSpace(value.Json) ? "null" : value.Json;
            writer.WriteRawValue(json, false);
        }

        private static void WriteCurrentValue(ref Utf8JsonReader reader, Utf8JsonWriter writer)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject:
                    writer.WriteStartObject();
                    while (reader.Read())
                    {
                        if (reader.TokenType == JsonTokenType.EndObject) break;
                        if (reader.TokenType != JsonTokenType.PropertyName) throw new JsonException("Expected JSON property name while reading raw JSON value.");
                        string propertyName = reader.GetString() ?? String.Empty;
                        writer.WritePropertyName(propertyName);
                        if (!reader.Read()) throw new JsonException("Expected JSON property value while reading raw JSON value.");
                        WriteCurrentValue(ref reader, writer);
                    }

                    writer.WriteEndObject();
                    break;
                case JsonTokenType.StartArray:
                    writer.WriteStartArray();
                    while (reader.Read())
                    {
                        if (reader.TokenType == JsonTokenType.EndArray) break;
                        WriteCurrentValue(ref reader, writer);
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
                    throw new JsonException("Unsupported JSON token while reading raw JSON value: " + reader.TokenType + ".");
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
    }
}

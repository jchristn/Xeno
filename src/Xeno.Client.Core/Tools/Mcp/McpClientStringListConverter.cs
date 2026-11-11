namespace Xeno.Client.Tools.Mcp
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Reads a JSON Schema value that may be one string or an array of scalars (for example "type": "string" or
    /// "type": ["string", "null"], and "enum": ["a", 1, true]) as a list of strings. Nested objects and arrays are skipped.
    /// </summary>
    internal sealed class McpClientStringListConverter : JsonConverter<List<string>>
    {
        /// <summary>
        /// Read the value.
        /// </summary>
        /// <param name="reader">Reader.</param>
        /// <param name="typeToConvert">Type.</param>
        /// <param name="options">Options.</param>
        /// <returns>The strings.</returns>
        public override List<string>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            List<string> values = new List<string>();
            if (reader.TokenType == JsonTokenType.StartArray)
            {
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    string? value = ReadScalar(ref reader);
                    if (value != null) values.Add(value);
                }

                return values;
            }

            string? single = ReadScalar(ref reader);
            if (single != null) values.Add(single);
            return values;
        }

        /// <summary>
        /// Write the value.
        /// </summary>
        /// <param name="writer">Writer.</param>
        /// <param name="value">Value.</param>
        /// <param name="options">Options.</param>
        public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            foreach (string item in value) writer.WriteStringValue(item);
            writer.WriteEndArray();
        }

        private static string? ReadScalar(ref Utf8JsonReader reader)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.String:
                    return reader.GetString();
                case JsonTokenType.Number:
                    return reader.GetDouble().ToString(CultureInfo.InvariantCulture);
                case JsonTokenType.True:
                    return "true";
                case JsonTokenType.False:
                    return "false";
                case JsonTokenType.StartObject:
                case JsonTokenType.StartArray:
                    reader.Skip();
                    return null;
                default:
                    return null;
            }
        }
    }
}

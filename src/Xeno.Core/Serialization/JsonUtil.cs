namespace Xeno.Core.Serialization
{
    using System;
    using System.IO;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// JSON serialization helper.
    /// </summary>
    public static class JsonUtil
    {
        private static readonly JsonSerializerOptions _Options = CreateOptions();

        /// <summary>
        /// Serialize an object to JSON.
        /// </summary>
        /// <param name="value">Object value.</param>
        /// <param name="indented">Whether to indent output.</param>
        /// <returns>JSON string.</returns>
        public static string Serialize(object? value, bool indented = false)
        {
            JsonSerializerOptions options = CreateOptions();
            options.WriteIndented = indented;
            return JsonSerializer.Serialize(value, options);
        }

        /// <summary>
        /// Deserialize JSON.
        /// </summary>
        /// <typeparam name="T">Target type.</typeparam>
        /// <param name="json">JSON string.</param>
        /// <returns>Deserialized value.</returns>
        public static T? Deserialize<T>(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<T>(json, _Options);
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException("JSON could not be deserialized as " + typeof(T).Name + ".", ex);
            }
        }

        /// <summary>
        /// Try to deserialize JSON.
        /// </summary>
        /// <typeparam name="T">Target type.</typeparam>
        /// <param name="json">JSON string.</param>
        /// <param name="value">Deserialized value.</param>
        /// <returns>True if the payload deserialized into a value.</returns>
        public static bool TryDeserialize<T>(string json, out T? value)
        {
            try
            {
                value = Deserialize<T>(json);
                return value != null;
            }
            catch (InvalidDataException)
            {
                value = default;
                return false;
            }
        }

        private static JsonSerializerOptions CreateOptions()
        {
            JsonSerializerOptions options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            options.Converters.Add(new JsonStringEnumConverter());
            return options;
        }
    }
}

namespace Xeno.Core.Serialization
{
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// JSON serialization helper.
    /// </summary>
    public static class JsonUtil
    {
        /// <summary>
        /// Shared JSON serializer options.
        /// </summary>
        public static JsonSerializerOptions Options { get; } = CreateOptions();

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
            return JsonSerializer.Deserialize<T>(json, Options);
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

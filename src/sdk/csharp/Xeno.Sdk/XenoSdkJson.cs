namespace Xeno.Sdk
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Internal JSON helper for SDK named-type serialization.
    /// </summary>
    internal static class XenoSdkJson
    {
        private static readonly JsonSerializerOptions _Options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = null,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// Serialize a named request object.
        /// </summary>
        /// <param name="value">Request object.</param>
        /// <returns>JSON payload.</returns>
        public static string Serialize(object value)
        {
            return JsonSerializer.Serialize(value, _Options);
        }

        /// <summary>
        /// Try to deserialize a response into a named type.
        /// </summary>
        /// <typeparam name="T">Named response type.</typeparam>
        /// <param name="json">JSON payload.</param>
        /// <param name="value">Deserialized value.</param>
        /// <returns>True if deserialization succeeded.</returns>
        public static bool TryDeserialize<T>(string json, out T? value)
        {
            try
            {
                value = JsonSerializer.Deserialize<T>(json, _Options);
                return value != null;
            }
            catch (JsonException)
            {
                value = default;
                return false;
            }
        }

        /// <summary>
        /// Deserialize a response into a named type.
        /// </summary>
        /// <typeparam name="T">Named response type.</typeparam>
        /// <param name="json">JSON payload.</param>
        /// <returns>Deserialized response.</returns>
        /// <exception cref="JsonException">Thrown when the payload cannot be deserialized into <typeparamref name="T"/>.</exception>
        public static T Deserialize<T>(string json)
        {
            T? value = JsonSerializer.Deserialize<T>(json, _Options);
            if (value == null) throw new JsonException("Xeno API response deserialized to null for type " + typeof(T).Name + ".");
            return value;
        }
    }
}

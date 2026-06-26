namespace Test.Shared
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// OpenAPI schema projection used by tests.
    /// </summary>
    public class TestOpenApiSchema
    {
        /// <summary>
        /// Referenced schema name.
        /// </summary>
        [JsonPropertyName("$ref")]
        public string? Reference { get; set; } = null;

        /// <summary>
        /// Schema type.
        /// </summary>
        [JsonPropertyName("type")]
        public string? Type { get; set; } = null;
    }
}

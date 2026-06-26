namespace Test.Shared
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// OpenAPI security scheme projection used by tests.
    /// </summary>
    public class TestOpenApiSecurityScheme
    {
        /// <summary>
        /// Header or query parameter name.
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; } = null;
    }
}

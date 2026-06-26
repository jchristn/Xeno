namespace Test.Shared
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// OpenAPI media type projection used by tests.
    /// </summary>
    public class TestOpenApiMediaType
    {
        /// <summary>
        /// Schema associated with the media type.
        /// </summary>
        [JsonPropertyName("schema")]
        public TestOpenApiSchema? Schema { get; set; } = null;
    }
}

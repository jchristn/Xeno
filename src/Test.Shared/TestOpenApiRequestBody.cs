namespace Test.Shared
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// OpenAPI request body projection used by tests.
    /// </summary>
    public class TestOpenApiRequestBody
    {
        /// <summary>
        /// Content definitions keyed by content type.
        /// </summary>
        [JsonPropertyName("content")]
        public Dictionary<string, TestOpenApiMediaType> Content { get; set; } = new Dictionary<string, TestOpenApiMediaType>();
    }
}

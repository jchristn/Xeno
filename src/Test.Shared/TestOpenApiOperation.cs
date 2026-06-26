namespace Test.Shared
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// OpenAPI operation projection used by tests.
    /// </summary>
    public class TestOpenApiOperation
    {
        /// <summary>
        /// Operation parameters.
        /// </summary>
        [JsonPropertyName("parameters")]
        public List<TestOpenApiParameter> Parameters { get; set; } = new List<TestOpenApiParameter>();

        /// <summary>
        /// Request body definition.
        /// </summary>
        [JsonPropertyName("requestBody")]
        public TestOpenApiRequestBody? RequestBody { get; set; } = null;
    }
}

namespace Test.Shared
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// OpenAPI document projection used by tests.
    /// </summary>
    public class TestOpenApiDocument
    {
        /// <summary>
        /// Path definitions keyed by route template.
        /// </summary>
        [JsonPropertyName("paths")]
        public Dictionary<string, Dictionary<string, TestOpenApiOperation>> Paths { get; set; } = new Dictionary<string, Dictionary<string, TestOpenApiOperation>>();

        /// <summary>
        /// Components section.
        /// </summary>
        [JsonPropertyName("components")]
        public TestOpenApiComponents Components { get; set; } = new TestOpenApiComponents();
    }
}

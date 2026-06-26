namespace Test.Shared
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// OpenAPI parameter projection used by tests.
    /// </summary>
    public class TestOpenApiParameter
    {
        /// <summary>
        /// Parameter name.
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; } = null;

        /// <summary>
        /// Parameter location.
        /// </summary>
        [JsonPropertyName("in")]
        public string? In { get; set; } = null;
    }
}

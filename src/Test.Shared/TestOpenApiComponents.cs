namespace Test.Shared
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// OpenAPI components projection used by tests.
    /// </summary>
    public class TestOpenApiComponents
    {
        /// <summary>
        /// Security scheme definitions keyed by scheme name.
        /// </summary>
        [JsonPropertyName("securitySchemes")]
        public Dictionary<string, TestOpenApiSecurityScheme> SecuritySchemes { get; set; } = new Dictionary<string, TestOpenApiSecurityScheme>();
    }
}

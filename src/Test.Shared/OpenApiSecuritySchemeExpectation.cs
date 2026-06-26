namespace Test.Shared
{
    /// <summary>
    /// Expected OpenAPI security scheme.
    /// </summary>
    internal sealed class OpenApiSecuritySchemeExpectation
    {
        /// <summary>
        /// Security scheme name.
        /// </summary>
        public string Scheme { get; set; } = string.Empty;

        /// <summary>
        /// Header name used by the scheme.
        /// </summary>
        public string Header { get; set; } = string.Empty;
    }
}

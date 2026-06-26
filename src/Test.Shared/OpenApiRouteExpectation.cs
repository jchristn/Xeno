namespace Test.Shared
{
    /// <summary>
    /// Expected OpenAPI route and method.
    /// </summary>
    internal sealed class OpenApiRouteExpectation
    {
        /// <summary>
        /// Route path.
        /// </summary>
        public string Route { get; set; } = string.Empty;

        /// <summary>
        /// HTTP method.
        /// </summary>
        public string Method { get; set; } = string.Empty;
    }
}

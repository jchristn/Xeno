namespace Test.Shared
{
    /// <summary>
    /// Expected OpenAPI parameter.
    /// </summary>
    internal sealed class OpenApiParameterExpectation
    {
        /// <summary>
        /// Route path.
        /// </summary>
        public string Route { get; set; } = string.Empty;

        /// <summary>
        /// HTTP method.
        /// </summary>
        public string Method { get; set; } = string.Empty;

        /// <summary>
        /// Parameter name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Parameter location.
        /// </summary>
        public string Location { get; set; } = string.Empty;
    }
}

namespace Xeno.Client.Tools
{
    /// <summary>
    /// Arguments shared by the Xeno tools. Each tool reads only the members its schema declares.
    /// </summary>
    public sealed class XenoToolArguments
    {
        /// <summary>
        /// Model name.
        /// </summary>
        public string? Model { get; set; } = null;

        /// <summary>
        /// Conversation id.
        /// </summary>
        public string? ConversationId { get; set; } = null;

        /// <summary>
        /// Text to search for.
        /// </summary>
        public string? Search { get; set; } = null;

        /// <summary>
        /// Most items to return.
        /// </summary>
        public int? Limit { get; set; } = null;

        /// <summary>
        /// Items to skip.
        /// </summary>
        public int? Skip { get; set; } = null;

        /// <summary>
        /// True to include Xeno network models.
        /// </summary>
        public bool? IncludeNetwork { get; set; } = null;

        /// <summary>
        /// Route rule target: "Local" or "Network".
        /// </summary>
        public string? Target { get; set; } = null;

        /// <summary>
        /// Local provider for a Local route rule.
        /// </summary>
        public string? Provider { get; set; } = null;

        /// <summary>
        /// True to let a Local route rule fall back to the network.
        /// </summary>
        public bool? FallbackToNetwork { get; set; } = null;
    }
}

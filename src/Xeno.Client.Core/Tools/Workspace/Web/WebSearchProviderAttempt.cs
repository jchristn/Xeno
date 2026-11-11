namespace Xeno.Client.Tools.Workspace.Web
{
    using System;

    /// <summary>
    /// One provider attempt in a web search.
    /// </summary>
    public sealed class WebSearchProviderAttempt
    {
        /// <summary>
        /// Provider name.
        /// </summary>
        public string ProviderName { get; set; } = String.Empty;

        /// <summary>
        /// Provider type.
        /// </summary>
        public string ProviderType { get; set; } = String.Empty;

        /// <summary>
        /// Whether it succeeded.
        /// </summary>
        public bool Success { get; set; } = false;

        /// <summary>
        /// Outcome message.
        /// </summary>
        public string Message { get; set; } = String.Empty;
    }
}

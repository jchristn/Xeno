namespace Xeno.Client
{
    using System;

    /// <summary>
    /// Web search for the chat web_search tool: Tavily or You.com, with the provider's API key.
    /// </summary>
    public class ChatWebSearchSettings
    {
        private int _MaxResults = 5;

        /// <summary>
        /// "None", "Tavily", or "You". The web_search tool is offered only when this names a provider with an API key.
        /// </summary>
        public string Provider { get; set; } = "None";

        /// <summary>
        /// Tavily API key, or null.
        /// </summary>
        public string? TavilyApiKey { get; set; } = null;

        /// <summary>
        /// You.com API key, or null.
        /// </summary>
        public string? YouApiKey { get; set; } = null;

        /// <summary>
        /// Results per search, 1 to 20.
        /// </summary>
        public int MaxResults
        {
            get { return _MaxResults; }
            set { _MaxResults = Math.Clamp(value, 1, 20); }
        }

        /// <summary>
        /// True when <see cref="Provider"/> names a provider and its API key is set.
        /// </summary>
        public bool IsConfigured
        {
            get
            {
                if (String.Equals(Provider, "Tavily", StringComparison.OrdinalIgnoreCase)) return !String.IsNullOrWhiteSpace(TavilyApiKey);
                if (String.Equals(Provider, "You", StringComparison.OrdinalIgnoreCase)) return !String.IsNullOrWhiteSpace(YouApiKey);
                return false;
            }
        }
    }
}

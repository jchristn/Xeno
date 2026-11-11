namespace Xeno.Client.Tools.Workspace.Web
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Chat.Wire;

    /// <summary>
    /// web_search, ported from Mux: searches the public web through the provider configured in
    /// <see cref="ClientChatSettings.WebSearch"/> (Tavily or You.com), read at call time. Queries and API keys are
    /// never logged.
    /// </summary>
    public sealed class WebSearchTool : IWorkspaceTool
    {
        #region Public-Members

        /// <summary>
        /// Tool name.
        /// </summary>
        public const string ToolName = "web_search";

        /// <inheritdoc />
        public string Name
        {
            get { return ToolName; }
        }

        /// <inheritdoc />
        public string Description
        {
            get { return "Searches the public web using configured external search providers and returns structured results with URLs and snippets."; }
        }

        /// <inheritdoc />
        public ChatToolSchemaDto Schema
        {
            get
            {
                ChatToolPropertyDto text = new ChatToolPropertyDto { Type = "string" };
                return WorkspaceSchema.Object(
                    WorkspaceSchema.Text("query", "The search query to run."),
                    WorkspaceSchema.Integer("max_results", "Maximum number of results to return. Defaults to the configured number (5 unless changed); at most 20."),
                    WorkspaceSchema.Text("provider", "Optional configured provider name or provider type to prefer for this request: tavily or you."),
                    WorkspaceSchema.Text("freshness", "Optional freshness or relative time filter such as day, week, or month when supported."),
                    WorkspaceSchema.Bool("include_answer", "Whether to request a provider-generated answer or summary when supported."),
                    WorkspaceSchema.Bool("include_images", "Whether to request images when supported."),
                    WorkspaceSchema.Integer("offset", "Optional result offset for paging when supported."),
                    WorkspaceSchema.Array("include_domains", "Optional domains to include.", text),
                    WorkspaceSchema.Array("exclude_domains", "Optional domains to exclude.", text),
                    "query");
            }
        }

        /// <inheritdoc />
        public bool Mutating
        {
            get { return false; }
        }

        #endregion

        #region Private-Members

        private readonly WebHttp _Http;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the tool.
        /// </summary>
        /// <param name="handler">HTTP handler for tests, or null for the real network.</param>
        public WebSearchTool(HttpMessageHandler? handler = null) : this(new WebHttp(handler))
        {
        }

        internal WebSearchTool(WebHttp http)
        {
            _Http = http ?? throw new ArgumentNullException(nameof(http));
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<object> InvokeAsync(string arguments, WorkspaceToolContext context, CancellationToken token)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            WebSearchArguments args = WorkspaceJson.Read<WebSearchArguments>(arguments);
            if (String.IsNullOrWhiteSpace(args.Query)) throw new ArgumentException("Required parameter 'query' is missing or not a string.");
            ChatWebSearchSettings settings = context.Settings.WebSearch;
            string providerType = SelectProvider(settings, args.Provider);
            WebSearchQuery query = new WebSearchQuery
            {
                Query = args.Query.Trim(),
                MaxResults = Math.Clamp(args.MaxResults ?? settings.MaxResults, 1, 20),
                Offset = Math.Clamp(args.Offset ?? 0, 0, 9),
                Freshness = String.IsNullOrWhiteSpace(args.Freshness) ? null : args.Freshness.Trim(),
                IncludeAnswer = args.IncludeAnswer ?? true,
                IncludeImages = args.IncludeImages,
                IncludeDomains = Normalize(args.IncludeDomains),
                ExcludeDomains = Normalize(args.ExcludeDomains)
            };

            HttpClient client = _Http.ForApi();
            WebSearchResult result = providerType == TavilySearchProvider.ProviderType
                ? await TavilySearchProvider.SearchAsync(client, settings.TavilyApiKey!.Trim(), query, token).ConfigureAwait(false)
                : await YouSearchProvider.SearchAsync(client, settings.YouApiKey!.Trim(), query, token).ConfigureAwait(false);
            result.UsedFallback = false;
            result.ProvidersTried = new List<string> { result.ProviderName };
            result.Attempts = new List<WebSearchProviderAttempt>
            {
                new WebSearchProviderAttempt { ProviderName = result.ProviderName, ProviderType = result.ProviderType, Success = true, Message = "Success." }
            };
            return result;
        }

        #endregion

        #region Private-Methods

        private static string SelectProvider(ChatWebSearchSettings settings, string? requested)
        {
            if (String.IsNullOrWhiteSpace(requested))
            {
                if (!settings.IsConfigured) throw new InvalidOperationException("Web search is not configured. Choose Tavily or You.com and set its API key in chat settings.");
                return NormalizeProvider(settings.Provider) ?? throw new InvalidOperationException("Web search is not configured.");
            }

            string? type = NormalizeProvider(requested);
            if (type == TavilySearchProvider.ProviderType && !String.IsNullOrWhiteSpace(settings.TavilyApiKey)) return type;
            if (type == YouSearchProvider.ProviderType && !String.IsNullOrWhiteSpace(settings.YouApiKey)) return type;
            throw new InvalidOperationException("No enabled search provider named '" + requested + "' is configured.");
        }

        private static string? NormalizeProvider(string? value)
        {
            string normalized = (value ?? String.Empty).Trim().ToLowerInvariant();
            if (normalized == "tavily") return TavilySearchProvider.ProviderType;
            if (normalized == "you" || normalized == "you.com" || normalized == "ydc") return YouSearchProvider.ProviderType;
            return null;
        }

        private static List<string> Normalize(List<string>? values)
        {
            List<string> result = new List<string>();
            if (values == null) return result;
            foreach (string value in values)
            {
                if (String.IsNullOrWhiteSpace(value)) continue;
                string trimmed = value.Trim();
                if (!result.Exists(item => String.Equals(item, trimmed, StringComparison.OrdinalIgnoreCase))) result.Add(trimmed);
            }

            return result;
        }

        #endregion
    }
}

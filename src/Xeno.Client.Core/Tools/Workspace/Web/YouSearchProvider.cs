namespace Xeno.Client.Tools.Workspace.Web
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// You.com search, ported from Mux.Search.
    /// </summary>
    internal static class YouSearchProvider
    {
        #region Public-Members

        /// <summary>
        /// You.com search endpoint.
        /// </summary>
        public const string Endpoint = "https://ydc-index.io/v1/search";

        /// <summary>
        /// Provider name.
        /// </summary>
        public const string ProviderName = "You.com";

        /// <summary>
        /// Provider type.
        /// </summary>
        public const string ProviderType = "you";

        #endregion

        #region Public-Methods

        /// <summary>
        /// Search.
        /// </summary>
        /// <param name="client">HTTP client.</param>
        /// <param name="apiKey">API key.</param>
        /// <param name="query">Query.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Result.</returns>
        /// <exception cref="ArgumentException">Both include_domains and exclude_domains are set.</exception>
        public static async Task<WebSearchResult> SearchAsync(HttpClient client, string apiKey, WebSearchQuery query, CancellationToken token)
        {
            if (query.IncludeDomains.Count > 0 && query.ExcludeDomains.Count > 0)
            {
                throw new ArgumentException("You.com requests should specify include_domains or exclude_domains, but not both.");
            }

            YouSearchRequest body = new YouSearchRequest
            {
                Query = query.Query,
                Count = query.MaxResults,
                Offset = query.Offset,
                Freshness = query.Freshness,
                IncludeDomains = query.IncludeDomains.Count > 0 ? query.IncludeDomains : null,
                ExcludeDomains = query.ExcludeDomains.Count > 0 ? query.ExcludeDomains : null
            };

            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, Endpoint))
            {
                request.Headers.Add("X-API-Key", apiKey);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                YouApiResponse response = await WebSearchHttp.SendAsync<YouSearchRequest, YouApiResponse>(client, request, body, ProviderName, token).ConfigureAwait(false);
                WebSearchResult result = new WebSearchResult
                {
                    ProviderName = ProviderName,
                    ProviderType = ProviderType,
                    Query = query.Query
                };

                if (response.Results != null)
                {
                    result.Sections["web"] = ConvertSection(response.Results.Web, "web");
                    result.Sections["news"] = ConvertSection(response.Results.News, "news");
                }

                if (response.Metadata != null)
                {
                    result.RequestId = response.Metadata.SearchUuid;
                    result.LatencySeconds = response.Metadata.Latency;
                }

                return result;
            }
        }

        #endregion

        #region Private-Methods

        private static List<WebSearchResultItem> ConvertSection(List<YouApiResult>? items, string section)
        {
            List<WebSearchResultItem> results = new List<WebSearchResultItem>();
            if (items == null) return results;
            foreach (YouApiResult item in items)
            {
                if (item == null) continue;
                List<string> snippets = item.Snippets ?? new List<string>();
                if (!String.IsNullOrWhiteSpace(item.Description) && !snippets.Contains(item.Description)) snippets.Insert(0, item.Description);
                results.Add(new WebSearchResultItem
                {
                    Section = section,
                    Title = item.Title ?? String.Empty,
                    Url = item.Url ?? String.Empty,
                    Description = item.Description,
                    Snippets = snippets,
                    FaviconUrl = item.FaviconUrl,
                    ThumbnailUrl = item.ThumbnailUrl,
                    RawContent = item.Content ?? item.Markdown ?? item.Html,
                    PublishedAt = item.PageAge
                });
            }

            return results;
        }

        #endregion
    }
}

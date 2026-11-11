namespace Xeno.Client.Tools.Workspace.Web
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Tavily search, ported from Mux.Search.
    /// </summary>
    internal static class TavilySearchProvider
    {
        #region Public-Members

        /// <summary>
        /// Tavily search endpoint.
        /// </summary>
        public const string Endpoint = "https://api.tavily.com/search";

        /// <summary>
        /// Provider name.
        /// </summary>
        public const string ProviderName = "Tavily";

        /// <summary>
        /// Provider type.
        /// </summary>
        public const string ProviderType = "tavily";

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
        public static async Task<WebSearchResult> SearchAsync(HttpClient client, string apiKey, WebSearchQuery query, CancellationToken token)
        {
            TavilySearchRequest body = new TavilySearchRequest
            {
                Query = query.Query,
                MaxResults = Math.Min(query.MaxResults, 20),
                TimeRange = query.Freshness,
                IncludeAnswer = query.IncludeAnswer ? "basic" : null,
                IncludeImages = query.IncludeImages,
                IncludeImageDescriptions = query.IncludeImages,
                IncludeDomains = query.IncludeDomains.Count > 0 ? query.IncludeDomains : null,
                ExcludeDomains = query.ExcludeDomains.Count > 0 ? query.ExcludeDomains : null
            };

            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, Endpoint))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                TavilyApiResponse response = await WebSearchHttp.SendAsync<TavilySearchRequest, TavilyApiResponse>(client, request, body, ProviderName, token).ConfigureAwait(false);
                WebSearchResult result = new WebSearchResult
                {
                    ProviderName = ProviderName,
                    ProviderType = ProviderType,
                    Query = response.Query ?? query.Query,
                    Answer = response.Answer,
                    RequestId = response.RequestId,
                    LatencySeconds = response.ResponseTime,
                    Images = ConvertImages(response.Images)
                };

                result.Sections["web"] = ConvertResults(response.Results);
                return result;
            }
        }

        #endregion

        #region Private-Methods

        private static List<WebSearchResultItem> ConvertResults(List<TavilyApiResult>? items)
        {
            List<WebSearchResultItem> results = new List<WebSearchResultItem>();
            if (items == null) return results;
            foreach (TavilyApiResult item in items)
            {
                if (item == null) continue;
                List<string> snippets = new List<string>();
                if (!String.IsNullOrWhiteSpace(item.Content)) snippets.Add(item.Content);
                results.Add(new WebSearchResultItem
                {
                    Section = "web",
                    Title = item.Title ?? String.Empty,
                    Url = item.Url ?? String.Empty,
                    Description = item.Content,
                    Snippets = snippets,
                    Score = item.Score,
                    RawContent = item.RawContent,
                    FaviconUrl = item.Favicon,
                    PublishedAt = item.PublishedDate,
                    Images = ConvertImages(item.Images)
                });
            }

            return results;
        }

        private static List<WebSearchImage> ConvertImages(List<TavilyApiImage>? items)
        {
            List<WebSearchImage> images = new List<WebSearchImage>();
            if (items == null) return images;
            foreach (TavilyApiImage image in items)
            {
                string? url = image?.Url ?? image?.ImageUrl;
                if (!String.IsNullOrWhiteSpace(url)) images.Add(new WebSearchImage { Url = url.Trim(), Description = image!.Description ?? image.Alt });
            }

            return images;
        }

        #endregion
    }
}

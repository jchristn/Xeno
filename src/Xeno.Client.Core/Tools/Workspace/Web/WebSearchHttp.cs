namespace Xeno.Client.Tools.Workspace.Web
{
    using System;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Sends a search provider request as JSON and reads the response into a named type. Neither the query nor the
    /// API key nor the response body is logged or put in error messages.
    /// </summary>
    internal static class WebSearchHttp
    {
        #region Private-Members

        private static readonly JsonSerializerOptions _Options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };

        private static readonly TimeSpan _Timeout = TimeSpan.FromSeconds(60);

        #endregion

        #region Public-Methods

        /// <summary>
        /// Send the request.
        /// </summary>
        /// <typeparam name="TRequest">Request body type.</typeparam>
        /// <typeparam name="TResponse">Response body type.</typeparam>
        /// <param name="client">HTTP client.</param>
        /// <param name="request">Request with URL and headers; its content is set here.</param>
        /// <param name="body">Request body.</param>
        /// <param name="providerName">Provider name for errors.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Response body.</returns>
        /// <exception cref="InvalidOperationException">The provider failed or returned something unreadable.</exception>
        public static async Task<TResponse> SendAsync<TRequest, TResponse>(HttpClient client, HttpRequestMessage request, TRequest body, string providerName, CancellationToken token) where TResponse : class
        {
            request.Content = new StringContent(JsonSerializer.Serialize(body, _Options), Encoding.UTF8, "application/json");
            using (CancellationTokenSource timeout = new CancellationTokenSource(_Timeout))
            using (CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(token, timeout.Token))
            {
                try
                {
                    using (HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, linked.Token).ConfigureAwait(false))
                    {
                        string json = await response.Content.ReadAsStringAsync(linked.Token).ConfigureAwait(false);
                        if (!response.IsSuccessStatusCode)
                        {
                            throw new InvalidOperationException("The " + providerName + " request failed with status code " + (int)response.StatusCode + " (" + response.ReasonPhrase + ").");
                        }

                        try
                        {
                            return JsonSerializer.Deserialize<TResponse>(json, _Options) ?? throw new InvalidOperationException("The " + providerName + " response body was empty.");
                        }
                        catch (JsonException)
                        {
                            throw new InvalidOperationException("The " + providerName + " response body was not valid JSON.");
                        }
                    }
                }
                catch (OperationCanceledException) when (timeout.IsCancellationRequested && !token.IsCancellationRequested)
                {
                    throw new TimeoutException("The " + providerName + " request took longer than " + (int)_Timeout.TotalSeconds + " seconds.");
                }
                catch (HttpRequestException ex)
                {
                    throw new InvalidOperationException("The " + providerName + " request failed: " + ex.Message);
                }
            }
        }

        #endregion
    }
}

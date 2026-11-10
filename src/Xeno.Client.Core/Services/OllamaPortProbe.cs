namespace Xeno.Client.Services
{
    using System;
    using System.Globalization;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Client.Telemetry.Models;
    using Xeno.Core.Serialization;

    /// <summary>
    /// Checks whether Ollama is answering on a loopback port, used to explain local inference port conflicts.
    /// Thread safe; the class is stateless.
    /// </summary>
    public static class OllamaPortProbe
    {
        /// <summary>
        /// Probe GET /api/version on 127.0.0.1 at the given port.
        /// </summary>
        /// <param name="port">Port to probe. Minimum 1, maximum 65535.</param>
        /// <param name="timeoutMs">Probe timeout in milliseconds. Default is 1000. Minimum 100.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The Ollama version when Ollama answered, otherwise null.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The port is outside 1 through 65535.</exception>
        public static async Task<string?> DetectOllamaAsync(int port, int timeoutMs = 1000, CancellationToken token = default)
        {
            if (port < 1 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1 and 65535.");
            string url = "http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + "/api/version";
            try
            {
                using (HttpClient client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(Math.Max(100, timeoutMs)) })
                using (HttpResponseMessage response = await client.GetAsync(url, token).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode) return null;
                    string json = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                    OllamaVersionResponse? version = JsonUtil.Deserialize<OllamaVersionResponse>(json);
                    return String.IsNullOrWhiteSpace(version?.Version) ? null : version!.Version;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is System.Text.Json.JsonException || ex is InvalidOperationException)
            {
                if (token.IsCancellationRequested) throw;
                return null;
            }
        }
    }
}

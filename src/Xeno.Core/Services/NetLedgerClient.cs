namespace Xeno.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Enums;
    using Xeno.Core.Models;
    using Xeno.Core.Serialization;
    using Xeno.Core.Settings;

    /// <summary>
    /// Narrow NetLedger REST client used by Xeno ledgering.
    /// </summary>
    public sealed class NetLedgerClient : IDisposable
    {
        #region Private-Members

        private readonly LedgerSettings _Settings;
        private readonly HttpClient _HttpClient;
        private readonly bool _OwnsHttpClient;
        private bool _Disposed;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the NetLedger client.
        /// </summary>
        /// <param name="settings">Ledger settings.</param>
        /// <param name="httpClient">Optional HTTP client.</param>
        public NetLedgerClient(LedgerSettings settings, HttpClient? httpClient = null)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _HttpClient = httpClient ?? new HttpClient();
            _OwnsHttpClient = httpClient == null;
            _HttpClient.Timeout = TimeSpan.FromMilliseconds(_Settings.RequestTimeoutMs);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Check NetLedger health.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if NetLedger returned success.</returns>
        public async Task<bool> HealthAsync(CancellationToken token = default)
        {
            using (HttpRequestMessage request = BuildRequest(HttpMethod.Get, "/"))
            {
                using (HttpResponseMessage response = await _HttpClient.SendAsync(request, token).ConfigureAwait(false))
                {
                    return response.IsSuccessStatusCode;
                }
            }
        }

        /// <summary>
        /// Create a tenant.
        /// </summary>
        /// <param name="tenant">Tenant.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created tenant.</returns>
        public async Task<NetLedgerTenant> CreateTenantAsync(NetLedgerTenant tenant, CancellationToken token = default)
        {
            if (tenant == null) throw new ArgumentNullException(nameof(tenant));
            using (HttpRequestMessage request = BuildJsonRequest(HttpMethod.Put, "/v1/tenants", tenant))
            {
                return await SendJsonAsync<NetLedgerTenant>(request, token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Delete a tenant.
        /// </summary>
        /// <param name="tenantId">NetLedger tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        public async Task DeleteTenantAsync(string tenantId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
            using (HttpRequestMessage request = BuildRequest(HttpMethod.Delete, "/v1/tenants/" + Escape(tenantId)))
            {
                await SendNoBodyAsync(request, token, true).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Create an account.
        /// </summary>
        /// <param name="tenantId">NetLedger tenant identifier.</param>
        /// <param name="requestBody">Create account request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created account.</returns>
        public async Task<NetLedgerAccount> CreateAccountAsync(string tenantId, NetLedgerCreateAccountRequest requestBody, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
            if (requestBody == null) throw new ArgumentNullException(nameof(requestBody));
            using (HttpRequestMessage request = BuildJsonRequest(HttpMethod.Put, "/v1/tenants/" + Escape(tenantId) + "/accounts", requestBody))
            {
                return await SendJsonAsync<NetLedgerAccount>(request, token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Delete an account and its entries.
        /// </summary>
        /// <param name="tenantId">NetLedger tenant identifier.</param>
        /// <param name="accountId">NetLedger account identifier.</param>
        /// <param name="token">Cancellation token.</param>
        public async Task DeleteAccountAsync(string tenantId, string accountId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
            if (String.IsNullOrWhiteSpace(accountId)) throw new ArgumentException("Account ID is required.", nameof(accountId));
            using (HttpRequestMessage request = BuildRequest(HttpMethod.Delete, "/v1/tenants/" + Escape(tenantId) + "/accounts/" + Escape(accountId)))
            {
                await SendNoBodyAsync(request, token, true).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Enumerate accounts by tags.
        /// </summary>
        /// <param name="tenantId">NetLedger tenant identifier.</param>
        /// <param name="tags">Tags that must all match.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Accounts.</returns>
        public async Task<List<NetLedgerAccount>> EnumerateAccountsByTagsAsync(string tenantId, Dictionary<string, string> tags, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
            string path = "/v1/tenants/" + Escape(tenantId) + "/accounts?maxResults=100&tags=" + Escape(FormatTags(tags));
            using (HttpRequestMessage request = BuildRequest(HttpMethod.Get, path))
            {
                NetLedgerEnumerationResult<NetLedgerAccount> result = await SendJsonAsync<NetLedgerEnumerationResult<NetLedgerAccount>>(request, token).ConfigureAwait(false);
                return result.Objects.Count > 0 ? result.Objects : result.Items;
            }
        }

        /// <summary>
        /// Get account balances.
        /// </summary>
        /// <param name="tenantId">NetLedger tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Balances.</returns>
        public async Task<List<NetLedgerBalance>> GetBalancesAsync(string tenantId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
            using (HttpRequestMessage request = BuildRequest(HttpMethod.Get, "/v1/tenants/" + Escape(tenantId) + "/balances"))
            {
                List<NetLedgerBalance>? balances = await SendJsonAsync<List<NetLedgerBalance>>(request, token).ConfigureAwait(false);
                return balances ?? new List<NetLedgerBalance>();
            }
        }

        /// <summary>
        /// Find account entries by tags.
        /// </summary>
        /// <param name="tenantId">NetLedger tenant identifier.</param>
        /// <param name="accountId">NetLedger account identifier.</param>
        /// <param name="tags">Tags that must all match.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Entries.</returns>
        public async Task<List<NetLedgerEntry>> EnumerateEntriesByTagsAsync(string tenantId, string accountId, Dictionary<string, string> tags, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
            if (String.IsNullOrWhiteSpace(accountId)) throw new ArgumentException("Account ID is required.", nameof(accountId));
            string path = "/v1/tenants/" + Escape(tenantId) + "/accounts/" + Escape(accountId) + "/entries?maxResults=100&tags=" + Escape(FormatTags(tags));
            using (HttpRequestMessage request = BuildRequest(HttpMethod.Get, path))
            {
                NetLedgerEnumerationResult<NetLedgerEntry> result = await SendJsonAsync<NetLedgerEnumerationResult<NetLedgerEntry>>(request, token).ConfigureAwait(false);
                return result.Objects.Count > 0 ? result.Objects : result.Items;
            }
        }

        /// <summary>
        /// Add a credit or debit entry.
        /// </summary>
        /// <param name="tenantId">NetLedger tenant identifier.</param>
        /// <param name="accountId">NetLedger account identifier.</param>
        /// <param name="direction">Entry direction.</param>
        /// <param name="requestBody">Create entry request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created entry identifiers.</returns>
        public async Task<NetLedgerCreateEntryResponse> AddEntryAsync(string tenantId, string accountId, LedgerEntryDirection direction, NetLedgerCreateEntryRequest requestBody, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
            if (String.IsNullOrWhiteSpace(accountId)) throw new ArgumentException("Account ID is required.", nameof(accountId));
            if (requestBody == null) throw new ArgumentNullException(nameof(requestBody));
            string route = direction == LedgerEntryDirection.Debit ? "/debits" : "/credits";
            using (HttpRequestMessage request = BuildJsonRequest(HttpMethod.Put, "/v1/tenants/" + Escape(tenantId) + "/accounts/" + Escape(accountId) + route, requestBody))
            {
                return await SendJsonAsync<NetLedgerCreateEntryResponse>(request, token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Dispose resources.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed) return;
            if (_OwnsHttpClient) _HttpClient.Dispose();
            _Disposed = true;
        }

        #endregion

        #region Private-Methods

        private HttpRequestMessage BuildRequest(HttpMethod method, string path)
        {
            Uri uri = new Uri(new Uri(_Settings.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute), path.TrimStart('/'));
            HttpRequestMessage request = new HttpRequestMessage(method, uri);
            if (!String.IsNullOrWhiteSpace(_Settings.ApiKey))
            {
                if (!String.IsNullOrWhiteSpace(_Settings.SecretKey))
                {
                    request.Headers.TryAddWithoutValidation("x-access-key", _Settings.ApiKey);
                    request.Headers.TryAddWithoutValidation("x-secret-key", _Settings.SecretKey);
                }
                else
                {
                    request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + _Settings.ApiKey);
                }
            }

            return request;
        }

        private HttpRequestMessage BuildJsonRequest(HttpMethod method, string path, object value)
        {
            HttpRequestMessage request = BuildRequest(method, path);
            request.Content = new StringContent(JsonUtil.Serialize(value), Encoding.UTF8, "application/json");
            return request;
        }

        private async Task<T> SendJsonAsync<T>(HttpRequestMessage request, CancellationToken token)
        {
            using (HttpResponseMessage response = await _HttpClient.SendAsync(request, token).ConfigureAwait(false))
            {
                string json = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException("NetLedger returned HTTP " + ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture) + ": " + json);
                }

                T? value = JsonUtil.Deserialize<T>(json);
                if (value == null) throw new InvalidOperationException("NetLedger response could not be deserialized as " + typeof(T).Name + ".");
                return value;
            }
        }

        private async Task SendNoBodyAsync(HttpRequestMessage request, CancellationToken token, bool ignoreNotFound = false)
        {
            using (HttpResponseMessage response = await _HttpClient.SendAsync(request, token).ConfigureAwait(false))
            {
                if (ignoreNotFound && response.StatusCode == HttpStatusCode.NotFound)
                {
                    return;
                }

                if (!response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                    throw new InvalidOperationException("NetLedger returned HTTP " + ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture) + ": " + json);
                }
            }
        }

        private static string FormatTags(Dictionary<string, string> tags)
        {
            if (tags == null || tags.Count == 0) return String.Empty;
            return String.Join(",", tags.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase).Select(item => item.Key + "=" + item.Value));
        }

        private static string Escape(string value)
        {
            return WebUtility.UrlEncode(value ?? String.Empty);
        }

        #endregion
    }
}

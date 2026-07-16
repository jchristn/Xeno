namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Enums;
    using Xeno.Core.Models;
    using Xeno.Core.Serialization;

    internal sealed class FakeNetLedgerServer : IDisposable
    {
        private readonly HttpListener _Listener = new HttpListener();
        private readonly CancellationTokenSource _Cancellation = new CancellationTokenSource();
        private readonly object _Lock = new object();
        private readonly Dictionary<string, NetLedgerTenant> _Tenants = new Dictionary<string, NetLedgerTenant>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, NetLedgerAccount> _Accounts = new Dictionary<string, NetLedgerAccount>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, NetLedgerEntry> _Entries = new Dictionary<string, NetLedgerEntry>(StringComparer.OrdinalIgnoreCase);
        private readonly Task _ListenTask;
        private bool _Disposed;

        internal FakeNetLedgerServer()
        {
            Port = GetFreePort();
            BaseUrl = "http://127.0.0.1:" + Port.ToString(CultureInfo.InvariantCulture);
            _Listener.Prefixes.Add(BaseUrl + "/");
            _Listener.Start();
            _ListenTask = Task.Run(async () => await ListenAsync(_Cancellation.Token).ConfigureAwait(false));
        }

        internal int Port { get; }

        internal string BaseUrl { get; }

        internal string RequiredAccessKey { get; set; } = "ledger-access";

        internal string RequiredSecretKey { get; set; } = "ledger-secret";

        internal string RequiredBearer { get; set; } = "ledger-bearer";

        internal bool RequireSecretKeyAuth { get; set; } = true;

        internal bool FailHealth { get; set; }

        internal int FailNextEntryCreates { get; set; }

        internal int TenantCreateCount { get; private set; }

        internal int AccountCreateCount { get; private set; }

        internal int AccountDeleteCount { get; private set; }

        internal int TenantDeleteCount { get; private set; }

        internal int EntryCreateCount { get; private set; }

        internal int EntryEnumerationCount { get; private set; }

        internal bool SawAccessKeySecretAuth { get; private set; }

        internal bool SawBearerAuth { get; private set; }

        internal List<NetLedgerTenant> Tenants
        {
            get
            {
                lock (_Lock)
                {
                    return _Tenants.Values.Select(CloneTenant).ToList();
                }
            }
        }

        internal List<NetLedgerAccount> Accounts
        {
            get
            {
                lock (_Lock)
                {
                    return _Accounts.Values.Select(CloneAccount).ToList();
                }
            }
        }

        internal List<NetLedgerEntry> Entries
        {
            get
            {
                lock (_Lock)
                {
                    return _Entries.Values.Select(CloneEntry).ToList();
                }
            }
        }

        internal NetLedgerTenant AddTenant(string id, string name)
        {
            NetLedgerTenant tenant = new NetLedgerTenant { Id = id, Name = name, Active = true };
            lock (_Lock)
            {
                _Tenants[id] = CloneTenant(tenant);
            }

            return tenant;
        }

        internal NetLedgerAccount AddAccount(string id, string tenantId, string name, List<string> labels, Dictionary<string, string> tags)
        {
            NetLedgerAccount account = new NetLedgerAccount
            {
                Id = id,
                TenantId = tenantId,
                Name = name,
                Labels = new List<string>(labels),
                Tags = new Dictionary<string, string>(tags, StringComparer.OrdinalIgnoreCase)
            };
            lock (_Lock)
            {
                _Accounts[id] = CloneAccount(account);
            }

            return account;
        }

        internal NetLedgerEntry AddEntry(string id, string tenantId, string accountId, LedgerEntryDirection direction, decimal amount, bool committed, Dictionary<string, string> tags)
        {
            NetLedgerEntry entry = new NetLedgerEntry
            {
                Id = id,
                TenantId = tenantId,
                AccountId = accountId,
                Type = direction == LedgerEntryDirection.Debit ? "Debit" : "Credit",
                Amount = amount,
                IsCommitted = committed,
                Labels = new List<string> { "xeno", direction == LedgerEntryDirection.Debit ? "xeno-debit" : "xeno-credit" },
                Tags = new Dictionary<string, string>(tags, StringComparer.OrdinalIgnoreCase),
                CreatedUtc = DateTime.UtcNow
            };
            lock (_Lock)
            {
                _Entries[id] = CloneEntry(entry);
            }

            return entry;
        }

        public void Dispose()
        {
            if (_Disposed) return;
            _Cancellation.Cancel();
            _Listener.Stop();
            _Listener.Close();
            try
            {
                _ListenTask.Wait(TimeSpan.FromSeconds(5));
            }
            catch (AggregateException)
            {
            }

            _Cancellation.Dispose();
            _Disposed = true;
        }

        private async Task ListenAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await _Listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (HttpListenerException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                _ = Task.Run(async () => await HandleAsync(context).ConfigureAwait(false), token);
            }
        }

        private async Task HandleAsync(HttpListenerContext context)
        {
            try
            {
                if (FailHealth && context.Request.HttpMethod == "GET" && context.Request.Url?.AbsolutePath == "/")
                {
                    await SendAsync(context, 503, new { Error = "unhealthy" }).ConfigureAwait(false);
                    return;
                }

                if (context.Request.HttpMethod == "GET" && context.Request.Url?.AbsolutePath == "/")
                {
                    await SendAsync(context, 200, new { Success = true }).ConfigureAwait(false);
                    return;
                }

                if (!Authenticate(context))
                {
                    await SendAsync(context, 401, new { Error = "Invalid API key" }).ConfigureAwait(false);
                    return;
                }

                string path = context.Request.Url?.AbsolutePath ?? String.Empty;
                string[] segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length == 2 && segments[0] == "v1" && segments[1] == "tenants" && context.Request.HttpMethod == "PUT")
                {
                    await CreateTenantAsync(context).ConfigureAwait(false);
                    return;
                }

                if (segments.Length == 3 && segments[0] == "v1" && segments[1] == "tenants" && context.Request.HttpMethod == "DELETE")
                {
                    bool deleted = DeleteTenant(segments[2]);
                    await SendAsync(context, deleted ? 200 : 404, new { Success = deleted }).ConfigureAwait(false);
                    return;
                }

                if (segments.Length == 4 && segments[0] == "v1" && segments[1] == "tenants" && segments[3] == "accounts" && context.Request.HttpMethod == "GET")
                {
                    await EnumerateAccountsAsync(context, segments[2]).ConfigureAwait(false);
                    return;
                }

                if (segments.Length == 4 && segments[0] == "v1" && segments[1] == "tenants" && segments[3] == "accounts" && context.Request.HttpMethod == "PUT")
                {
                    await CreateAccountAsync(context, segments[2]).ConfigureAwait(false);
                    return;
                }

                if (segments.Length == 5 && segments[0] == "v1" && segments[1] == "tenants" && segments[3] == "accounts" && context.Request.HttpMethod == "DELETE")
                {
                    bool deleted = DeleteAccount(segments[4]);
                    await SendAsync(context, deleted ? 200 : 404, new { Success = deleted }).ConfigureAwait(false);
                    return;
                }

                if (segments.Length == 6 && segments[0] == "v1" && segments[1] == "tenants" && segments[3] == "accounts" && segments[5] == "entries" && context.Request.HttpMethod == "GET")
                {
                    await EnumerateEntriesAsync(context, segments[2], segments[4]).ConfigureAwait(false);
                    return;
                }

                if (segments.Length == 6 && segments[0] == "v1" && segments[1] == "tenants" && segments[3] == "accounts" && (segments[5] == "credits" || segments[5] == "debits") && context.Request.HttpMethod == "PUT")
                {
                    await CreateEntryAsync(context, segments[2], segments[4], segments[5] == "debits" ? LedgerEntryDirection.Debit : LedgerEntryDirection.Credit).ConfigureAwait(false);
                    return;
                }

                if (segments.Length == 6 && segments[0] == "v1" && segments[1] == "tenants" && segments[3] == "accounts" && segments[5] == "balance" && context.Request.HttpMethod == "GET")
                {
                    await GetBalanceAsync(context, segments[2], segments[4]).ConfigureAwait(false);
                    return;
                }

                await SendAsync(context, 404, new { Error = "not found" }).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                await SendAsync(context, 500, new { Error = ex.Message }).ConfigureAwait(false);
            }
        }

        private bool Authenticate(HttpListenerContext context)
        {
            string accessKey = context.Request.Headers["x-access-key"] ?? String.Empty;
            string secretKey = context.Request.Headers["x-secret-key"] ?? String.Empty;
            string authorization = context.Request.Headers["Authorization"] ?? String.Empty;
            if (RequireSecretKeyAuth)
            {
                bool matched = String.Equals(accessKey, RequiredAccessKey, StringComparison.Ordinal) && String.Equals(secretKey, RequiredSecretKey, StringComparison.Ordinal);
                SawAccessKeySecretAuth = SawAccessKeySecretAuth || matched;
                return matched;
            }

            bool bearerMatched = String.Equals(authorization, "Bearer " + RequiredBearer, StringComparison.Ordinal);
            SawBearerAuth = SawBearerAuth || bearerMatched;
            return bearerMatched;
        }

        private async Task CreateTenantAsync(HttpListenerContext context)
        {
            NetLedgerTenant tenant = await ReadBodyAsync<NetLedgerTenant>(context).ConfigureAwait(false);
            if (String.IsNullOrWhiteSpace(tenant.Id)) tenant.Id = "ten_fake_" + (TenantCreateCount + 1).ToString(CultureInfo.InvariantCulture);
            if (String.IsNullOrWhiteSpace(tenant.Name)) tenant.Name = tenant.Id;
            lock (_Lock)
            {
                TenantCreateCount++;
                _Tenants[tenant.Id] = CloneTenant(tenant);
            }

            await SendAsync(context, 201, tenant).ConfigureAwait(false);
        }

        private bool DeleteTenant(string tenantId)
        {
            lock (_Lock)
            {
                TenantDeleteCount++;
                return _Tenants.Remove(tenantId);
            }
        }

        private async Task CreateAccountAsync(HttpListenerContext context, string tenantId)
        {
            NetLedgerCreateAccountRequest request = await ReadBodyAsync<NetLedgerCreateAccountRequest>(context).ConfigureAwait(false);
            NetLedgerAccount account = new NetLedgerAccount
            {
                Id = "acct_fake_" + (AccountCreateCount + 1).ToString(CultureInfo.InvariantCulture),
                TenantId = tenantId,
                Name = request.Name,
                Labels = new List<string>(request.Labels),
                Tags = new Dictionary<string, string>(request.Tags, StringComparer.OrdinalIgnoreCase)
            };
            lock (_Lock)
            {
                AccountCreateCount++;
                _Accounts[account.Id] = CloneAccount(account);
            }

            await SendAsync(context, 201, account).ConfigureAwait(false);
        }

        private async Task EnumerateAccountsAsync(HttpListenerContext context, string tenantId)
        {
            Dictionary<string, string> tags = ParseTags(context.Request.QueryString["tags"]);
            List<NetLedgerAccount> accounts;
            lock (_Lock)
            {
                accounts = _Accounts.Values
                    .Where(item => String.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
                    .Where(item => TagsMatch(item.Tags, tags))
                    .Select(CloneAccount)
                    .ToList();
            }

            await SendAsync(context, 200, new NetLedgerEnumerationResult<NetLedgerAccount>
            {
                Objects = accounts,
                TotalRecords = accounts.Count,
                EndOfResults = true
            }).ConfigureAwait(false);
        }

        private bool DeleteAccount(string accountId)
        {
            lock (_Lock)
            {
                AccountDeleteCount++;
                bool deleted = _Accounts.Remove(accountId);
                List<string> entryIds = _Entries.Values.Where(item => String.Equals(item.AccountId, accountId, StringComparison.OrdinalIgnoreCase)).Select(item => item.Id).ToList();
                foreach (string entryId in entryIds)
                {
                    _Entries.Remove(entryId);
                }

                return deleted;
            }
        }

        private async Task EnumerateEntriesAsync(HttpListenerContext context, string tenantId, string accountId)
        {
            Dictionary<string, string> tags = ParseTags(context.Request.QueryString["tags"]);
            List<NetLedgerEntry> entries;
            lock (_Lock)
            {
                EntryEnumerationCount++;
                entries = _Entries.Values
                    .Where(item => String.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
                    .Where(item => String.Equals(item.AccountId, accountId, StringComparison.OrdinalIgnoreCase))
                    .Where(item => TagsMatch(item.Tags, tags))
                    .Select(CloneEntry)
                    .ToList();
            }

            await SendAsync(context, 200, new NetLedgerEnumerationResult<NetLedgerEntry>
            {
                Objects = entries,
                TotalRecords = entries.Count,
                EndOfResults = true
            }).ConfigureAwait(false);
        }

        private async Task CreateEntryAsync(HttpListenerContext context, string tenantId, string accountId, LedgerEntryDirection direction)
        {
            lock (_Lock)
            {
                if (FailNextEntryCreates > 0)
                {
                    FailNextEntryCreates--;
                    throw new InvalidOperationException("configured entry create failure");
                }
            }

            NetLedgerCreateEntryRequest request = await ReadBodyAsync<NetLedgerCreateEntryRequest>(context).ConfigureAwait(false);
            string id;
            NetLedgerEntry entry;
            lock (_Lock)
            {
                EntryCreateCount++;
                id = "ent_fake_" + EntryCreateCount.ToString(CultureInfo.InvariantCulture);
                entry = new NetLedgerEntry
                {
                    Id = id,
                    TenantId = tenantId,
                    AccountId = accountId,
                    Type = direction == LedgerEntryDirection.Debit ? "Debit" : "Credit",
                    Amount = request.Amount,
                    Description = request.Notes,
                    IsCommitted = request.IsCommitted,
                    Labels = new List<string>(request.Labels),
                    Tags = new Dictionary<string, string>(request.Tags, StringComparer.OrdinalIgnoreCase),
                    CreatedUtc = DateTime.UtcNow
                };
                _Entries[id] = CloneEntry(entry);
            }

            await SendAsync(context, 201, new NetLedgerCreateEntryResponse { EntryIds = new List<string> { id } }).ConfigureAwait(false);
        }

        private async Task GetBalanceAsync(HttpListenerContext context, string tenantId, string accountId)
        {
            decimal committed = 0m;
            lock (_Lock)
            {
                foreach (NetLedgerEntry entry in _Entries.Values.Where(item => String.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase) && String.Equals(item.AccountId, accountId, StringComparison.OrdinalIgnoreCase) && item.IsCommitted))
                {
                    committed += String.Equals(entry.Type, "Debit", StringComparison.OrdinalIgnoreCase) ? -entry.Amount : entry.Amount;
                }
            }

            await SendAsync(context, 200, new { AccountId = accountId, CommittedBalance = committed, PendingBalance = committed }).ConfigureAwait(false);
        }

        private static async Task<T> ReadBodyAsync<T>(HttpListenerContext context) where T : class, new()
        {
            using (StreamReader reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
            {
                string json = await reader.ReadToEndAsync().ConfigureAwait(false);
                if (String.IsNullOrWhiteSpace(json)) return new T();
                T? value = JsonUtil.Deserialize<T>(json);
                if (value == null) throw new InvalidOperationException("Request body could not be deserialized as " + typeof(T).Name + ".");
                return value;
            }
        }

        private static async Task SendAsync(HttpListenerContext context, int statusCode, object value)
        {
            string json = JsonUtil.Serialize(value);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
            context.Response.Close();
        }

        private static Dictionary<string, string> ParseTags(string? value)
        {
            Dictionary<string, string> tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (String.IsNullOrWhiteSpace(value)) return tags;
            string[] pairs = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (string pair in pairs)
            {
                string[] parts = pair.Split('=', 2, StringSplitOptions.TrimEntries);
                if (parts.Length == 2 && !String.IsNullOrWhiteSpace(parts[0]))
                {
                    tags[parts[0]] = parts[1];
                }
            }

            return tags;
        }

        private static bool TagsMatch(Dictionary<string, string> actual, Dictionary<string, string> required)
        {
            foreach (KeyValuePair<string, string> pair in required)
            {
                if (!actual.TryGetValue(pair.Key, out string? actualValue)) return false;
                if (!String.Equals(actualValue, pair.Value, StringComparison.Ordinal)) return false;
            }

            return true;
        }

        private static NetLedgerTenant CloneTenant(NetLedgerTenant tenant)
        {
            return new NetLedgerTenant { Id = tenant.Id, Name = tenant.Name, Active = tenant.Active };
        }

        private static NetLedgerAccount CloneAccount(NetLedgerAccount account)
        {
            return new NetLedgerAccount
            {
                Id = account.Id,
                TenantId = account.TenantId,
                Name = account.Name,
                Labels = new List<string>(account.Labels),
                Tags = new Dictionary<string, string>(account.Tags, StringComparer.OrdinalIgnoreCase)
            };
        }

        private static NetLedgerEntry CloneEntry(NetLedgerEntry entry)
        {
            return new NetLedgerEntry
            {
                Id = entry.Id,
                TenantId = entry.TenantId,
                AccountId = entry.AccountId,
                Type = entry.Type,
                Amount = entry.Amount,
                Description = entry.Description,
                IsCommitted = entry.IsCommitted,
                Labels = new List<string>(entry.Labels),
                Tags = new Dictionary<string, string>(entry.Tags, StringComparer.OrdinalIgnoreCase),
                CreatedUtc = entry.CreatedUtc
            };
        }

        private static int GetFreePort()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            try
            {
                listener.Start();
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }
    }
}

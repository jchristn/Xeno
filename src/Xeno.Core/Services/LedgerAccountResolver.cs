namespace Xeno.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Models;
    using Xeno.Core.Settings;

    /// <summary>
    /// Resolves and creates NetLedger tenant and account links.
    /// </summary>
    public class LedgerAccountResolver
    {
        #region Private-Members

        private readonly XenoPlatform _Platform;
        private readonly LedgerSettings _Settings;
        private readonly NetLedgerClient _Client;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the resolver.
        /// </summary>
        /// <param name="platform">Platform state.</param>
        /// <param name="settings">Ledger settings.</param>
        /// <param name="client">NetLedger client.</param>
        public LedgerAccountResolver(XenoPlatform platform, LedgerSettings settings, NetLedgerClient client)
        {
            _Platform = platform ?? throw new ArgumentNullException(nameof(platform));
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Client = client ?? throw new ArgumentNullException(nameof(client));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Resolve or create a tenant link.
        /// </summary>
        /// <param name="xenoTenantId">Xeno tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Tenant link.</returns>
        public async Task<LedgerTenantLink> ResolveTenantAsync(string xenoTenantId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(xenoTenantId)) throw new ArgumentException("Tenant ID is required.", nameof(xenoTenantId));
            token.ThrowIfCancellationRequested();

            LedgerTenantLink? existing = _Platform.LedgerTenantLinks.FirstOrDefault(item => String.Equals(item.XenoTenantId, xenoTenantId, StringComparison.OrdinalIgnoreCase));
            if (existing != null) return existing;
            if (!_Settings.CreateTenantsAutomatically) throw new InvalidOperationException("Ledger tenant link does not exist and automatic tenant creation is disabled.");

            Tenant? xenoTenant = _Platform.Tenants.FirstOrDefault(item => String.Equals(item.Id, xenoTenantId, StringComparison.OrdinalIgnoreCase));
            if (xenoTenant == null) throw new InvalidOperationException("Xeno tenant '" + xenoTenantId + "' was not found.");

            NetLedgerTenant created = await _Client.CreateTenantAsync(new NetLedgerTenant
            {
                Id = Xeno.Core.Helpers.IdGenerator.NewId("ten_"),
                Name = xenoTenant.Name,
                Active = xenoTenant.Active
            }, token).ConfigureAwait(false);

            LedgerTenantLink link = new LedgerTenantLink
            {
                XenoTenantId = xenoTenantId,
                NetLedgerTenantId = created.Id,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            };
            _Platform.LedgerTenantLinks.Add(link);
            return link;
        }

        /// <summary>
        /// Resolve or create an account link.
        /// </summary>
        /// <param name="xenoTenantId">Xeno tenant identifier.</param>
        /// <param name="xenoUserId">Xeno user identifier.</param>
        /// <param name="tenantLink">Tenant link.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Account link.</returns>
        public async Task<LedgerAccountLink> ResolveAccountAsync(string xenoTenantId, string xenoUserId, LedgerTenantLink tenantLink, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(xenoTenantId)) throw new ArgumentException("Tenant ID is required.", nameof(xenoTenantId));
            if (String.IsNullOrWhiteSpace(xenoUserId)) throw new ArgumentException("User ID is required.", nameof(xenoUserId));
            if (tenantLink == null) throw new ArgumentNullException(nameof(tenantLink));
            token.ThrowIfCancellationRequested();

            LedgerAccountLink? existing = _Platform.LedgerAccountLinks.FirstOrDefault(item =>
                String.Equals(item.XenoTenantId, xenoTenantId, StringComparison.OrdinalIgnoreCase)
                && String.Equals(item.XenoUserId, xenoUserId, StringComparison.OrdinalIgnoreCase));
            if (existing != null) return existing;

            User? user = _Platform.Users.FirstOrDefault(item =>
                String.Equals(item.TenantId, xenoTenantId, StringComparison.OrdinalIgnoreCase)
                && String.Equals(item.Id, xenoUserId, StringComparison.OrdinalIgnoreCase));
            if (user == null) throw new InvalidOperationException("Xeno user '" + xenoUserId + "' was not found.");

            Dictionary<string, string> tags = LedgerMetadataBuilder.BuildAccountTags(xenoTenantId, user);
            List<NetLedgerAccount> matches = await _Client.EnumerateAccountsByTagsAsync(tenantLink.NetLedgerTenantId, tags, token).ConfigureAwait(false);
            NetLedgerAccount? matched = matches.FirstOrDefault(item => TagsMatch(item.Tags, tags));
            if (matched != null)
            {
                return AddAccountLink(xenoTenantId, xenoUserId, tenantLink.NetLedgerTenantId, matched.Id);
            }

            if (!_Settings.CreateAccountsAutomatically) throw new InvalidOperationException("Ledger account link does not exist and automatic account creation is disabled.");

            NetLedgerAccount created = await _Client.CreateAccountAsync(tenantLink.NetLedgerTenantId, new NetLedgerCreateAccountRequest
            {
                Name = "xeno:user:" + xenoUserId,
                InitialBalance = 0m,
                Labels = LedgerMetadataBuilder.BuildAccountLabels(),
                Tags = tags
            }, token).ConfigureAwait(false);

            return AddAccountLink(xenoTenantId, xenoUserId, tenantLink.NetLedgerTenantId, created.Id);
        }

        #endregion

        #region Private-Methods

        private LedgerAccountLink AddAccountLink(string xenoTenantId, string xenoUserId, string netLedgerTenantId, string netLedgerAccountId)
        {
            LedgerAccountLink link = new LedgerAccountLink
            {
                XenoTenantId = xenoTenantId,
                XenoUserId = xenoUserId,
                NetLedgerTenantId = netLedgerTenantId,
                NetLedgerAccountId = netLedgerAccountId,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            };
            _Platform.LedgerAccountLinks.Add(link);
            return link;
        }

        private static bool TagsMatch(Dictionary<string, string> actual, Dictionary<string, string> expected)
        {
            foreach (KeyValuePair<string, string> pair in expected)
            {
                if (!actual.TryGetValue(pair.Key, out string? actualValue)) return false;
                if (!String.Equals(actualValue, pair.Value, StringComparison.Ordinal)) return false;
            }

            return true;
        }

        #endregion
    }
}

namespace Xeno.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using SyslogLogging;
    using Xeno.Core.Database;
    using Xeno.Core.Models;
    using Xeno.Core.Settings;

    /// <summary>
    /// NetLedger tenant lifecycle subordinate service.
    /// </summary>
    public sealed class NetLedgerTenantSubordinateService : ITenantSubordinateService, IDisposable
    {
        private readonly DatabaseDriverBase _Database;
        private readonly LedgerSettings _Settings;
        private readonly LoggingModule _Logging;
        private readonly string _Header = "[NetLedgerTenantSubordinate] ";
        private readonly NetLedgerClient _Client;
        private readonly LedgerAccountResolver _Resolver;
        private bool _Disposed;

        /// <summary>
        /// Instantiate the service.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="settings">Ledger settings.</param>
        /// <param name="logging">Logging module.</param>
        public NetLedgerTenantSubordinateService(DatabaseDriverBase database, LedgerSettings settings, LoggingModule logging)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _Client = new NetLedgerClient(_Settings, _Logging);
            _Resolver = new LedgerAccountResolver(_Database, _Settings, _Client);
        }

        /// <inheritdoc />
        public string ServiceName => "NetLedger";

        /// <inheritdoc />
        public async Task<TenantSubordinateServiceStatus> CreateAsync(Tenant tenant, IReadOnlyList<User> seedUsers, bool allowDeferredProvisioning, CancellationToken token = default)
        {
            if (tenant == null) throw new ArgumentNullException(nameof(tenant));
            if (!_Settings.Enabled) return BuildStatus("create", tenant, "Skipped", "Ledgering is disabled.");

            try
            {
                LedgerTenantLink link = await _Resolver.ResolveTenantAsync(tenant.Id, token).ConfigureAwait(false);
                foreach (User user in seedUsers.Where(item => String.Equals(item.TenantId, tenant.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    await _Resolver.ResolveAccountAsync(tenant.Id, user.Id, link, token).ConfigureAwait(false);
                }

                _Logging.Info(_Header + "provisioned netledger tenant " + link.NetLedgerTenantId + " for tenant " + tenant.Id);
                return BuildStatus("create", tenant, "Provisioned", null, link.NetLedgerTenantId);
            }
            catch (Exception ex) when (allowDeferredProvisioning && IsProvisioningFailure(ex))
            {
                LedgerTenantLink? link = await FindLinkAsync(tenant.Id, token).ConfigureAwait(false);
                return BuildStatus("create", tenant, "Deferred", ex.Message, link?.NetLedgerTenantId);
            }
        }

        /// <inheritdoc />
        public async Task<TenantSubordinateServiceStatus> UpdateAsync(Tenant tenant, CancellationToken token = default)
        {
            if (tenant == null) throw new ArgumentNullException(nameof(tenant));
            if (!_Settings.Enabled) return BuildStatus("update", tenant, "Skipped", "Ledgering is disabled.");
            LedgerTenantLink? link = await FindLinkAsync(tenant.Id, token).ConfigureAwait(false);
            if (link == null) return BuildStatus("update", tenant, "Deferred", "NetLedger tenant link is not provisioned.");
            return await UpdateNetLedgerTenantAsync(tenant, link, "update", token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<TenantSubordinateServiceStatus> SuspendAsync(Tenant tenant, CancellationToken token = default)
        {
            if (tenant == null) throw new ArgumentNullException(nameof(tenant));
            if (!_Settings.Enabled) return BuildStatus("suspend", tenant, "Skipped", "Ledgering is disabled.");
            LedgerTenantLink? link = await FindLinkAsync(tenant.Id, token).ConfigureAwait(false);
            if (link == null) return BuildStatus("suspend", tenant, "Deferred", "NetLedger tenant link is not provisioned.");
            return await UpdateNetLedgerTenantAsync(tenant, link, "suspend", token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<TenantSubordinateServiceStatus> ReactivateAsync(Tenant tenant, CancellationToken token = default)
        {
            if (tenant == null) throw new ArgumentNullException(nameof(tenant));
            if (!_Settings.Enabled) return BuildStatus("reactivate", tenant, "Skipped", "Ledgering is disabled.");
            LedgerTenantLink? link = await FindLinkAsync(tenant.Id, token).ConfigureAwait(false);
            if (link == null) return BuildStatus("reactivate", tenant, "Deferred", "NetLedger tenant link is not provisioned.");
            return await UpdateNetLedgerTenantAsync(tenant, link, "reactivate", token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<TenantSubordinateServiceStatus> DeleteAsync(Tenant tenant, bool hardDelete, CancellationToken token = default)
        {
            if (tenant == null) throw new ArgumentNullException(nameof(tenant));
            if (!_Settings.Enabled) return BuildStatus("delete", tenant, "Skipped", "Ledgering is disabled.");

            LedgerTenantLink? link = await FindLinkAsync(tenant.Id, token).ConfigureAwait(false);
            if (link == null) return BuildStatus("delete", tenant, "Provisioned", "No NetLedger tenant link exists.");

            if (!hardDelete)
            {
                tenant.Active = false;
                return await SuspendAsync(tenant, token).ConfigureAwait(false);
            }

            List<LedgerAccountLink> tenantAccountLinks = await _Database.LedgerAccountLinks!.EnumerateByTenantAsync(tenant.Id, token).ConfigureAwait(false);
            List<string> accountIds = tenantAccountLinks
                .Select(item => item.NetLedgerAccountId)
                .Where(item => !String.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            Dictionary<string, string> tenantTags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "source", "xeno" },
                { "xenoTenantId", tenant.Id }
            };
            List<NetLedgerAccount> taggedAccounts = await _Client.EnumerateAccountsByTagsAsync(link.NetLedgerTenantId, tenantTags, token).ConfigureAwait(false);
            foreach (NetLedgerAccount account in taggedAccounts)
            {
                if (!accountIds.Contains(account.Id, StringComparer.OrdinalIgnoreCase))
                {
                    accountIds.Add(account.Id);
                }
            }

            foreach (string accountId in accountIds)
            {
                await _Client.DeleteAccountAsync(link.NetLedgerTenantId, accountId, token).ConfigureAwait(false);
            }

            await _Client.DeleteTenantAsync(link.NetLedgerTenantId, token).ConfigureAwait(false);
            _Logging.Info(_Header + "deleted netledger tenant " + link.NetLedgerTenantId + " for tenant " + tenant.Id);
            await _Database.LedgerTenantLinks!.DeleteByTenantAsync(tenant.Id, token).ConfigureAwait(false);
            await _Database.LedgerAccountLinks!.DeleteByTenantAsync(tenant.Id, token).ConfigureAwait(false);
            return BuildStatus("delete", tenant, "Deleted", null, link.NetLedgerTenantId);
        }

        /// <inheritdoc />
        public async Task<TenantSubordinateServiceStatus> ReconcileAsync(Tenant tenant, CancellationToken token = default)
        {
            if (tenant == null) throw new ArgumentNullException(nameof(tenant));
            if (!_Settings.Enabled) return BuildStatus("reconcile", tenant, "Skipped", "Ledgering is disabled.");
            LedgerTenantLink? link = await FindLinkAsync(tenant.Id, token).ConfigureAwait(false);
            if (link == null) return BuildStatus("reconcile", tenant, "Missing", "NetLedger tenant link is missing.");

            List<User> tenantUsers = await _Database.Users!.EnumerateAsync(tenant.Id, token).ConfigureAwait(false);
            List<LedgerAccountLink> accountLinks = await _Database.LedgerAccountLinks!.EnumerateByTenantAsync(tenant.Id, token).ConfigureAwait(false);
            List<User> missingAccounts = tenantUsers
                .Where(item => !accountLinks.Any(linkItem =>
                    String.Equals(linkItem.XenoTenantId, tenant.Id, StringComparison.OrdinalIgnoreCase) &&
                    String.Equals(linkItem.XenoUserId, item.Id, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            string? error = missingAccounts.Count == 0 ? null : missingAccounts.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + " account link(s) are missing.";
            return BuildStatus("reconcile", tenant, missingAccounts.Count == 0 ? "Provisioned" : "Partial", error, link.NetLedgerTenantId);
        }

        /// <summary>
        /// Dispose resources.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed) return;
            _Client.Dispose();
            _Disposed = true;
        }

        private async Task<TenantSubordinateServiceStatus> UpdateNetLedgerTenantAsync(Tenant tenant, LedgerTenantLink link, string operation, CancellationToken token)
        {
            NetLedgerTenant updated = await _Client.CreateTenantAsync(new NetLedgerTenant
            {
                Id = link.NetLedgerTenantId,
                Name = tenant.Name,
                Active = tenant.Active
            }, token).ConfigureAwait(false);
            return BuildStatus(operation, tenant, updated.Active == tenant.Active ? "Provisioned" : "Partial", null, link.NetLedgerTenantId);
        }

        private Task<LedgerTenantLink?> FindLinkAsync(string tenantId, CancellationToken token)
        {
            return _Database.LedgerTenantLinks!.ReadByTenantAsync(tenantId, token);
        }

        private TenantSubordinateServiceStatus BuildStatus(string operation, Tenant tenant, string status, string? error, string? externalTenantId = null)
        {
            return new TenantSubordinateServiceStatus
            {
                ServiceName = ServiceName,
                Operation = operation,
                XenoTenantId = tenant.Id,
                ExternalTenantId = externalTenantId,
                Status = status,
                ErrorMessage = error,
                UpdatedUtc = DateTime.UtcNow
            };
        }

        private static bool IsProvisioningFailure(Exception ex)
        {
            return ex is InvalidOperationException || ex is System.Net.Http.HttpRequestException || ex is TaskCanceledException;
        }
    }
}

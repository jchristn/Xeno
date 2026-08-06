namespace Xeno.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using SyslogLogging;
    using Xeno.Core.Database;
    using Xeno.Core.Database.Interfaces;
    using Xeno.Core.Enums;
    using Xeno.Core.Models;

    /// <summary>
    /// Coordinates tenant lifecycle calls across subordinate services.
    /// </summary>
    public class TenantLifecycleService
    {
        private readonly DatabaseDriverBase _Database;
        private readonly LoggingModule? _Logging;
        private readonly string _Header = "[TenantLifecycle] ";
        private readonly List<ITenantSubordinateService> _SubordinateServices;
        private readonly IAuditMethods? _Audit;

        /// <summary>
        /// Instantiate the service.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="subordinateServices">Subordinate services.</param>
        /// <param name="logging">Optional logging module.</param>
        /// <param name="audit">Optional audit table methods for persisting lifecycle audit records.</param>
        public TenantLifecycleService(DatabaseDriverBase database, IEnumerable<ITenantSubordinateService> subordinateServices, LoggingModule? logging = null, IAuditMethods? audit = null)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Logging = logging;
            _SubordinateServices = subordinateServices == null ? new List<ITenantSubordinateService>() : subordinateServices.ToList();
            _Audit = audit;
        }

        /// <summary>
        /// Create subordinate tenant state.
        /// </summary>
        /// <param name="tenant">Tenant.</param>
        /// <param name="seedUsers">Seed users.</param>
        /// <param name="allowDeferredProvisioning">Whether provisioning failures can be reported instead of thrown.</param>
        /// <param name="context">Optional request context for audit records.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Result.</returns>
        public async Task<TenantLifecycleResult> CreateAsync(Tenant tenant, IReadOnlyList<User> seedUsers, bool allowDeferredProvisioning, RequestContext? context = null, CancellationToken token = default)
        {
            return await RunAsync("create", tenant, context, service => service.CreateAsync(tenant, seedUsers, allowDeferredProvisioning, token), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Update subordinate tenant state.
        /// </summary>
        /// <param name="tenant">Tenant.</param>
        /// <param name="context">Optional request context for audit records.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Result.</returns>
        public async Task<TenantLifecycleResult> UpdateAsync(Tenant tenant, RequestContext? context = null, CancellationToken token = default)
        {
            return await RunAsync("update", tenant, context, service => service.UpdateAsync(tenant, token), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Suspend subordinate tenant state.
        /// </summary>
        /// <param name="tenant">Tenant.</param>
        /// <param name="context">Optional request context for audit records.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Result.</returns>
        public async Task<TenantLifecycleResult> SuspendAsync(Tenant tenant, RequestContext? context = null, CancellationToken token = default)
        {
            return await RunAsync("suspend", tenant, context, service => service.SuspendAsync(tenant, token), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Reactivate subordinate tenant state.
        /// </summary>
        /// <param name="tenant">Tenant.</param>
        /// <param name="context">Optional request context for audit records.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Result.</returns>
        public async Task<TenantLifecycleResult> ReactivateAsync(Tenant tenant, RequestContext? context = null, CancellationToken token = default)
        {
            return await RunAsync("reactivate", tenant, context, service => service.ReactivateAsync(tenant, token), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete subordinate tenant state.
        /// </summary>
        /// <param name="tenant">Tenant.</param>
        /// <param name="hardDelete">Whether data must be permanently removed.</param>
        /// <param name="context">Optional request context for audit records.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Result.</returns>
        public async Task<TenantLifecycleResult> DeleteAsync(Tenant tenant, bool hardDelete, RequestContext? context = null, CancellationToken token = default)
        {
            return await RunAsync("delete", tenant, context, service => service.DeleteAsync(tenant, hardDelete, token), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Reconcile subordinate tenant state.
        /// </summary>
        /// <param name="tenant">Tenant.</param>
        /// <param name="context">Optional request context for audit records.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Result.</returns>
        public async Task<TenantLifecycleResult> ReconcileAsync(Tenant tenant, RequestContext? context = null, CancellationToken token = default)
        {
            return await RunAsync("reconcile", tenant, context, service => service.ReconcileAsync(tenant, token), token).ConfigureAwait(false);
        }

        private async Task<TenantLifecycleResult> RunAsync(string operation, Tenant tenant, RequestContext? context, Func<ITenantSubordinateService, Task<TenantSubordinateServiceStatus>> execute, CancellationToken token)
        {
            if (tenant == null) throw new ArgumentNullException(nameof(tenant));
            TenantLifecycleResult result = new TenantLifecycleResult();
            foreach (ITenantSubordinateService service in _SubordinateServices)
            {
                token.ThrowIfCancellationRequested();
                TenantSubordinateServiceStatus status = await execute(service).ConfigureAwait(false);
                UpsertStatus(tenant, status);
                result.SubordinateServices.Add(status);
                if (IsFailure(status))
                {
                    result.Success = false;
                    _Logging?.Warn(_Header + "tenant " + tenant.Id + " " + operation + " subordinate " + service.ServiceName + " reported " + status.Status + (String.IsNullOrWhiteSpace(status.ErrorMessage) ? String.Empty : ": " + status.ErrorMessage));
                }

                await AppendAuditAsync(context, service.ServiceName, operation, tenant.Id, status, token).ConfigureAwait(false);
            }

            return result;
        }

        private static bool IsFailure(TenantSubordinateServiceStatus status)
        {
            return !String.Equals(status.Status, "Provisioned", StringComparison.OrdinalIgnoreCase)
                && !String.Equals(status.Status, "Deleted", StringComparison.OrdinalIgnoreCase)
                && !String.Equals(status.Status, "Skipped", StringComparison.OrdinalIgnoreCase);
        }

        private static void UpsertStatus(Tenant tenant, TenantSubordinateServiceStatus status)
        {
            TenantSubordinateServiceStatus? existing = tenant.SubordinateServices.FirstOrDefault(item => String.Equals(item.ServiceName, status.ServiceName, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                tenant.SubordinateServices.Add(status);
                return;
            }

            existing.Operation = status.Operation;
            existing.XenoTenantId = status.XenoTenantId;
            existing.ExternalTenantId = status.ExternalTenantId;
            existing.Status = status.Status;
            existing.ErrorMessage = status.ErrorMessage;
            existing.UpdatedUtc = status.UpdatedUtc;
        }

        private async Task AppendAuditAsync(RequestContext? context, string serviceName, string operation, string tenantId, TenantSubordinateServiceStatus status, CancellationToken token)
        {
            if (context == null || _Audit == null) return;

            AuditRecord record = new AuditRecord
            {
                TenantId = context.TenantId,
                TargetTenantId = tenantId,
                TargetResourceId = status.ExternalTenantId,
                RequestId = context.RequestId,
                PrincipalType = context.PrincipalType,
                PrincipalId = context.PrincipalId,
                AuthScheme = context.AuthScheme,
                ResourceType = "TenantLifecycle." + serviceName,
                OperationType = OperationTypeEnum.Admin,
                Allowed = !IsFailure(status),
                ReasonCode = operation + ":" + status.Status,
                SourceIp = context.SourceIp,
                ForwardedFor = context.ForwardedFor,
                UserAgent = context.UserAgent,
                Origin = context.Origin,
                Referer = context.Referer,
                Host = context.Host,
                RequestMethod = context.RequestMethod,
                RequestPath = context.RequestPath,
                CorrelationId = context.CorrelationId,
                StatusCode = IsFailure(status) ? 500 : 200,
                CreatedUtc = status.UpdatedUtc
            };

            try
            {
                await _Audit.InsertAsync(record, token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _Logging?.Warn(_Header + "failed to persist tenant lifecycle audit record: " + ex.Message);
            }
        }
    }
}

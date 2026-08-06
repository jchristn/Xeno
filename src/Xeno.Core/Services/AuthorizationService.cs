namespace Xeno.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Database;
    using Xeno.Core.Enums;
    using Xeno.Core.Models;

    /// <summary>
    /// Evaluates tenant, administrator, and RBAC authorization decisions.
    /// </summary>
    public class AuthorizationService
    {
        /// <summary>
        /// Authorize a protected operation.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="request">Authorization request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Authorization decision.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="database"/> or <paramref name="request"/> is null.</exception>
        public async Task<AuthorizationDecision> AuthorizeAsync(DatabaseDriverBase database, AuthorizationRequest request, CancellationToken token = default)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.Context == null) throw new ArgumentNullException(nameof(request.Context));

            RequestContext context = request.Context;
            AuthorizationDecision decision = CreateDecision(request);
            if (!context.IsAuthenticated)
            {
                decision.StatusCode = 401;
                decision.ReasonCode = "Unauthenticated";
                decision.Message = "Authentication is required.";
                return decision;
            }

            Tenant? targetTenant = await database.Tenants!.ReadAsync(request.TargetTenantId, token).ConfigureAwait(false);
            if (targetTenant == null || !targetTenant.Active)
            {
                decision.StatusCode = 404;
                decision.ReasonCode = "TenantNotFound";
                decision.Message = "Tenant was not found.";
                return decision;
            }

            User? user = await ResolveUserAsync(database, context, token).ConfigureAwait(false);
            if (context.PrincipalType == PrincipalTypeEnum.User && user == null)
            {
                decision.StatusCode = 401;
                decision.ReasonCode = "InvalidUser";
                decision.Message = "Authenticated user is inactive or missing.";
                return decision;
            }

            Credential? credential = await ResolveCredentialAsync(database, context, token).ConfigureAwait(false);
            if (context.PrincipalType == PrincipalTypeEnum.Credential && credential == null)
            {
                decision.StatusCode = 401;
                decision.ReasonCode = "InvalidCredential";
                decision.Message = "Authenticated credential is inactive or missing.";
                return decision;
            }

            if (CanUseUserAdminBypass(context) && user != null && user.IsAdmin)
            {
                if (request.AllowSystemAdminCrossTenant || SameTenant(context.TenantId, request.TargetTenantId))
                {
                    return Allow(decision, "SystemAdmin", "System administrator bypass.");
                }
            }

            if (!SameTenant(context.TenantId, request.TargetTenantId))
            {
                decision.StatusCode = 403;
                decision.ReasonCode = "TenantMismatch";
                decision.Message = "Authenticated tenant cannot access the requested tenant.";
                return decision;
            }

            if (CanUseUserAdminBypass(context) && user != null && user.IsTenantAdmin && SameTenant(user.TenantId, request.TargetTenantId))
            {
                return Allow(decision, "TenantAdmin", "Tenant administrator bypass.");
            }

            List<EffectivePermission> permissions = await GetEffectivePermissionsAsync(database, context, request.TargetTenantId, token).ConfigureAwait(false);
            EffectivePermission? explicitDeny = permissions.FirstOrDefault(item =>
                !item.Permit &&
                Matches(item.ResourceType, request.ResourceType) &&
                MatchesOperation(item.OperationType, request.OperationType));
            if (explicitDeny != null)
            {
                decision.StatusCode = 403;
                decision.ReasonCode = "PermissionDeniedExplicit";
                decision.Message = "Permission was explicitly denied.";
                decision.MatchedSource = explicitDeny.SourceRole;
                return decision;
            }

            EffectivePermission? permit = permissions.FirstOrDefault(item =>
                item.Permit &&
                Matches(item.ResourceType, request.ResourceType) &&
                MatchesOperation(item.OperationType, request.OperationType));
            if (permit != null)
            {
                return Allow(decision, permit.SourceRole, "Permission granted.");
            }

            decision.StatusCode = 403;
            decision.ReasonCode = "PermissionDenied";
            decision.Message = "Permission denied.";
            return decision;
        }

        /// <summary>
        /// Resolve effective permissions for a request context and target tenant.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="context">Request context.</param>
        /// <param name="targetTenantId">Target tenant ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Effective permissions.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="database"/> or <paramref name="context"/> is null.</exception>
        public async Task<List<EffectivePermission>> GetEffectivePermissionsAsync(DatabaseDriverBase database, RequestContext context, string targetTenantId, CancellationToken token = default)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (context == null) throw new ArgumentNullException(nameof(context));

            User? user = await ResolveUserAsync(database, context, token).ConfigureAwait(false);
            if (CanUseUserAdminBypass(context) && user != null && user.IsAdmin)
            {
                return AllPermissions("SystemAdmin");
            }

            if (CanUseUserAdminBypass(context) && user != null && user.IsTenantAdmin && SameTenant(user.TenantId, targetTenantId))
            {
                return AllPermissions("TenantAdmin");
            }

            string? principalId = context.PrincipalType == PrincipalTypeEnum.Credential ? context.CredentialId : context.UserId;
            if (String.IsNullOrWhiteSpace(principalId)) return new List<EffectivePermission>();

            List<RoleAssignment> allAssignments = await database.RoleAssignments!.EnumerateByPrincipalAsync(targetTenantId, principalId, token).ConfigureAwait(false);
            List<RoleAssignment> assignments = new List<RoleAssignment>();
            foreach (RoleAssignment assignment in allAssignments)
            {
                if (!assignment.Active) continue;
                if (assignment.PrincipalType != context.PrincipalType) continue;
                assignments.Add(assignment);
            }

            List<EffectivePermission> permissions = new List<EffectivePermission>();
            foreach (RoleAssignment assignment in assignments)
            {
                Role? role = await database.Roles!.ReadAsync(targetTenantId, assignment.RoleId, token).ConfigureAwait(false);
                if (role == null || !role.Active) continue;
                foreach (string permissionId in role.PermissionIds)
                {
                    Permission? permission = await database.Permissions!.ReadAsync(targetTenantId, permissionId, token).ConfigureAwait(false);
                    if (permission == null || !permission.Active) continue;
                    permissions.Add(new EffectivePermission
                    {
                        ResourceType = permission.ResourceType,
                        OperationType = permission.OperationType,
                        Permit = permission.Permit,
                        SourceRole = role.Name
                    });
                }
            }

            return permissions;
        }

        /// <summary>
        /// Determine whether an authenticated user is a system administrator.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="context">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when the context belongs to an active system administrator.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="database"/> or <paramref name="context"/> is null.</exception>
        public async Task<bool> IsSystemAdminAsync(DatabaseDriverBase database, RequestContext context, CancellationToken token = default)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (context == null) throw new ArgumentNullException(nameof(context));

            User? user = await ResolveUserAsync(database, context, token).ConfigureAwait(false);
            if (CanUseUserAdminBypass(context) && user != null && user.IsAdmin) return true;
            if (context.PrincipalType != PrincipalTypeEnum.Credential) return false;

            Credential? credential = await ResolveCredentialAsync(database, context, token).ConfigureAwait(false);
            if (credential == null || !String.Equals(credential.UserId, context.UserId, StringComparison.Ordinal)) return false;
            return user != null && user.IsAdmin;
        }

        /// <summary>
        /// Determine whether an authenticated user is a tenant administrator for a tenant.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="context">Request context.</param>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when the context belongs to an active tenant administrator for the tenant.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="database"/> or <paramref name="context"/> is null.</exception>
        public async Task<bool> IsTenantAdminAsync(DatabaseDriverBase database, RequestContext context, string tenantId, CancellationToken token = default)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (context == null) throw new ArgumentNullException(nameof(context));

            User? user = await ResolveUserAsync(database, context, token).ConfigureAwait(false);
            return CanUseUserAdminBypass(context) && user != null && user.IsTenantAdmin && SameTenant(user.TenantId, tenantId);
        }

        /// <summary>
        /// Determine whether a target tenant is visible to a request context.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="context">Request context.</param>
        /// <param name="targetTenantId">Target tenant ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when the caller may access the target tenant.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="database"/> or <paramref name="context"/> is null.</exception>
        public async Task<bool> CanAccessTenantAsync(DatabaseDriverBase database, RequestContext context, string targetTenantId, CancellationToken token = default)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (context == null) throw new ArgumentNullException(nameof(context));

            if (await IsSystemAdminAsync(database, context, token).ConfigureAwait(false)) return true;
            if (!SameTenant(context.TenantId, targetTenantId)) return false;
            User? user = await ResolveUserAsync(database, context, token).ConfigureAwait(false);
            if (user != null && !SameTenant(user.TenantId, targetTenantId)) return false;
            Credential? credential = await ResolveCredentialAsync(database, context, token).ConfigureAwait(false);
            if (credential != null && !SameTenant(credential.TenantId, targetTenantId)) return false;
            return true;
        }

        private static AuthorizationDecision Allow(AuthorizationDecision decision, string source, string message)
        {
            decision.Allowed = true;
            decision.StatusCode = 200;
            decision.ReasonCode = "Allowed";
            decision.Message = message;
            decision.MatchedSource = source;
            return decision;
        }

        private static AuthorizationDecision CreateDecision(AuthorizationRequest request)
        {
            return new AuthorizationDecision
            {
                TenantId = request.TargetTenantId,
                ResourceType = request.ResourceType,
                OperationType = request.OperationType,
                PrincipalType = request.Context.PrincipalType,
                PrincipalId = request.Context.PrincipalId
            };
        }

        private static List<EffectivePermission> AllPermissions(string source)
        {
            return new List<EffectivePermission>
            {
                new EffectivePermission { ResourceType = "*", OperationType = OperationTypeEnum.Read, Permit = true, SourceRole = source },
                new EffectivePermission { ResourceType = "*", OperationType = OperationTypeEnum.Write, Permit = true, SourceRole = source },
                new EffectivePermission { ResourceType = "*", OperationType = OperationTypeEnum.Delete, Permit = true, SourceRole = source },
                new EffectivePermission { ResourceType = "*", OperationType = OperationTypeEnum.Admin, Permit = true, SourceRole = source },
                new EffectivePermission { ResourceType = "*", OperationType = OperationTypeEnum.Execute, Permit = true, SourceRole = source }
            };
        }

        private static bool Matches(string candidate, string resourceType)
        {
            return String.Equals(candidate, "*", StringComparison.OrdinalIgnoreCase)
                || String.Equals(candidate, "All", StringComparison.OrdinalIgnoreCase)
                || String.Equals(candidate, resourceType, StringComparison.OrdinalIgnoreCase);
        }

        private static bool MatchesOperation(OperationTypeEnum candidate, OperationTypeEnum operationType)
        {
            if (candidate == operationType) return true;
            if (candidate == OperationTypeEnum.Admin && operationType == OperationTypeEnum.Read) return true;
            return candidate == OperationTypeEnum.Write && (operationType == OperationTypeEnum.Write || operationType == OperationTypeEnum.Delete);
        }

        private static bool SameTenant(string? left, string? right)
        {
            return String.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static bool CanUseUserAdminBypass(RequestContext context)
        {
            return context.PrincipalType == PrincipalTypeEnum.User || context.PrincipalType == PrincipalTypeEnum.Administrator;
        }

        private static async Task<User?> ResolveUserAsync(DatabaseDriverBase database, RequestContext context, CancellationToken token)
        {
            if (String.IsNullOrWhiteSpace(context.UserId)) return null;
            User? user = await database.Users!.ReadAsync(context.TenantId, context.UserId!, token).ConfigureAwait(false);
            if (user == null || !user.Active) return null;
            return user;
        }

        private static async Task<Credential?> ResolveCredentialAsync(DatabaseDriverBase database, RequestContext context, CancellationToken token)
        {
            if (String.IsNullOrWhiteSpace(context.CredentialId)) return null;
            Credential? credential = await database.Credentials!.ReadAsync(context.TenantId, context.CredentialId!, token).ConfigureAwait(false);
            if (credential == null || !credential.Active) return null;
            return credential;
        }
    }
}

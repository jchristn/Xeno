namespace Xeno.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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
        /// <param name="platform">Platform state.</param>
        /// <param name="request">Authorization request.</param>
        /// <returns>Authorization decision.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="platform"/> or <paramref name="request"/> is null.</exception>
        public AuthorizationDecision Authorize(XenoPlatform platform, AuthorizationRequest request)
        {
            if (platform == null) throw new ArgumentNullException(nameof(platform));
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

            Tenant? targetTenant = platform.Tenants.FirstOrDefault(item => item.Id == request.TargetTenantId && item.Active);
            if (targetTenant == null)
            {
                decision.StatusCode = 404;
                decision.ReasonCode = "TenantNotFound";
                decision.Message = "Tenant was not found.";
                return decision;
            }

            User? user = ResolveUser(platform, context);
            if (context.PrincipalType == PrincipalTypeEnum.User && user == null)
            {
                decision.StatusCode = 401;
                decision.ReasonCode = "InvalidUser";
                decision.Message = "Authenticated user is inactive or missing.";
                return decision;
            }

            Credential? credential = ResolveCredential(platform, context);
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

            List<EffectivePermission> permissions = GetEffectivePermissions(platform, context, request.TargetTenantId);
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
        /// <param name="platform">Platform state.</param>
        /// <param name="context">Request context.</param>
        /// <param name="targetTenantId">Target tenant ID.</param>
        /// <returns>Effective permissions.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="platform"/> or <paramref name="context"/> is null.</exception>
        public List<EffectivePermission> GetEffectivePermissions(XenoPlatform platform, RequestContext context, string targetTenantId)
        {
            if (platform == null) throw new ArgumentNullException(nameof(platform));
            if (context == null) throw new ArgumentNullException(nameof(context));

            User? user = ResolveUser(platform, context);
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

            List<RoleAssignment> assignments = platform.RoleAssignments
                .Where(item => item.TenantId == targetTenantId)
                .Where(item => item.Active)
                .Where(item => item.PrincipalType == context.PrincipalType)
                .Where(item => item.PrincipalId == principalId)
                .ToList();

            List<EffectivePermission> permissions = new List<EffectivePermission>();
            foreach (RoleAssignment assignment in assignments)
            {
                Role? role = platform.Roles.FirstOrDefault(item => item.Id == assignment.RoleId && item.TenantId == targetTenantId && item.Active);
                if (role == null) continue;
                foreach (string permissionId in role.PermissionIds)
                {
                    Permission? permission = platform.Permissions.FirstOrDefault(item => item.Id == permissionId && item.TenantId == targetTenantId && item.Active);
                    if (permission == null) continue;
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
        /// <param name="platform">Platform state.</param>
        /// <param name="context">Request context.</param>
        /// <returns>True when the context belongs to an active system administrator.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="platform"/> or <paramref name="context"/> is null.</exception>
        public bool IsSystemAdmin(XenoPlatform platform, RequestContext context)
        {
            if (platform == null) throw new ArgumentNullException(nameof(platform));
            if (context == null) throw new ArgumentNullException(nameof(context));

            User? user = ResolveUser(platform, context);
            if (CanUseUserAdminBypass(context) && user != null && user.IsAdmin) return true;
            if (context.PrincipalType != PrincipalTypeEnum.Credential) return false;

            Credential? credential = ResolveCredential(platform, context);
            if (credential == null || !String.Equals(credential.UserId, context.UserId, StringComparison.Ordinal)) return false;
            return user != null && user.IsAdmin;
        }

        /// <summary>
        /// Determine whether an authenticated user is a tenant administrator for a tenant.
        /// </summary>
        /// <param name="platform">Platform state.</param>
        /// <param name="context">Request context.</param>
        /// <param name="tenantId">Tenant ID.</param>
        /// <returns>True when the context belongs to an active tenant administrator for the tenant.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="platform"/> or <paramref name="context"/> is null.</exception>
        public bool IsTenantAdmin(XenoPlatform platform, RequestContext context, string tenantId)
        {
            if (platform == null) throw new ArgumentNullException(nameof(platform));
            if (context == null) throw new ArgumentNullException(nameof(context));

            User? user = ResolveUser(platform, context);
            return CanUseUserAdminBypass(context) && user != null && user.IsTenantAdmin && SameTenant(user.TenantId, tenantId);
        }

        /// <summary>
        /// Determine whether a target tenant is visible to a request context.
        /// </summary>
        /// <param name="platform">Platform state.</param>
        /// <param name="context">Request context.</param>
        /// <param name="targetTenantId">Target tenant ID.</param>
        /// <returns>True when the caller may access the target tenant.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="platform"/> or <paramref name="context"/> is null.</exception>
        public bool CanAccessTenant(XenoPlatform platform, RequestContext context, string targetTenantId)
        {
            if (platform == null) throw new ArgumentNullException(nameof(platform));
            if (context == null) throw new ArgumentNullException(nameof(context));

            if (IsSystemAdmin(platform, context)) return true;
            if (!SameTenant(context.TenantId, targetTenantId)) return false;
            User? user = ResolveUser(platform, context);
            if (user != null && !SameTenant(user.TenantId, targetTenantId)) return false;
            Credential? credential = ResolveCredential(platform, context);
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

        private static User? ResolveUser(XenoPlatform platform, RequestContext context)
        {
            if (String.IsNullOrWhiteSpace(context.UserId)) return null;
            return platform.Users.FirstOrDefault(item => item.Id == context.UserId && item.Active);
        }

        private static Credential? ResolveCredential(XenoPlatform platform, RequestContext context)
        {
            if (String.IsNullOrWhiteSpace(context.CredentialId)) return null;
            return platform.Credentials.FirstOrDefault(item => item.Id == context.CredentialId && item.Active);
        }
    }
}

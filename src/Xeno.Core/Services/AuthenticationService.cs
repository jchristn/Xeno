namespace Xeno.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using Xeno.Core.Enums;
    using Xeno.Core.Models;
    using Xeno.Core.Settings;

    /// <summary>
    /// Authentication, session, effective-permission, and audit helper service.
    /// </summary>
    public class AuthenticationService
    {
        /// <summary>
        /// Issue a bearer session token.
        /// </summary>
        /// <param name="platform">Platform state.</param>
        /// <param name="settings">Authentication settings.</param>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="principalType">Principal type.</param>
        /// <param name="userId">User ID.</param>
        /// <param name="credentialId">Credential ID.</param>
        /// <param name="authScheme">Authentication scheme.</param>
        /// <returns>Token issuance result.</returns>
        public AuthTokenResult IssueToken(
            XenoPlatform platform,
            AuthSettings settings,
            string tenantId,
            PrincipalTypeEnum principalType,
            string? userId,
            string? credentialId,
            AuthSchemeEnum authScheme)
        {
            if (platform == null) throw new ArgumentNullException(nameof(platform));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));

            string token = GenerateToken();
            DateTime expiresUtc = DateTime.UtcNow.AddMinutes(Math.Max(1, settings.SessionMinutes));
            AuthSession session = new AuthSession
            {
                TenantId = tenantId,
                PrincipalType = principalType,
                UserId = userId,
                CredentialId = credentialId,
                TokenHash = HashToken(token),
                AuthScheme = authScheme,
                ExpiresUtc = expiresUtc,
                Active = true
            };
            platform.AuthSessions.Add(session);

            return new AuthTokenResult
            {
                Token = token,
                TenantId = tenantId,
                SessionId = session.Id,
                ExpiresUtc = expiresUtc
            };
        }

        /// <summary>
        /// Validate a bearer token.
        /// </summary>
        /// <param name="platform">Platform state.</param>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="token">Bearer token.</param>
        /// <returns>Active auth session.</returns>
        public AuthSession? ValidateToken(XenoPlatform platform, string tenantId, string token)
        {
            if (platform == null) throw new ArgumentNullException(nameof(platform));
            if (String.IsNullOrWhiteSpace(token)) return null;

            string tokenHash = HashToken(token);
            DateTime now = DateTime.UtcNow;
            if (!platform.Tenants.Any(item => item.Id == tenantId && item.Active)) return null;
            AuthSession? session = platform.AuthSessions.FirstOrDefault(item =>
                item.TenantId == tenantId &&
                item.Active &&
                item.ExpiresUtc > now &&
                String.Equals(item.TokenHash, tokenHash, StringComparison.Ordinal));

            if (session == null) return null;
            if (!String.IsNullOrWhiteSpace(session.UserId))
            {
                User? user = platform.Users.FirstOrDefault(item => item.Id == session.UserId && item.TenantId == session.TenantId && item.Active);
                if (user == null) return null;
            }

            if (!String.IsNullOrWhiteSpace(session.CredentialId))
            {
                Credential? credential = platform.Credentials.FirstOrDefault(item => item.Id == session.CredentialId && item.TenantId == session.TenantId && item.Active);
                if (credential == null) return null;
            }

            return session;
        }

        /// <summary>
        /// Validate a direct credential.
        /// </summary>
        /// <param name="platform">Platform state.</param>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="accessKey">Access key.</param>
        /// <param name="secretKey">Secret key.</param>
        /// <returns>Credential when valid.</returns>
        public Credential? ValidateCredential(XenoPlatform platform, string tenantId, string accessKey, string secretKey)
        {
            if (platform == null) throw new ArgumentNullException(nameof(platform));
            if (String.IsNullOrWhiteSpace(accessKey) || String.IsNullOrWhiteSpace(secretKey)) return null;

            Credential? credential = platform.Credentials.FirstOrDefault(item =>
                item.TenantId == tenantId &&
                item.Active &&
                String.Equals(item.AccessKey, accessKey, StringComparison.Ordinal));
            if (credential == null) return null;
            if (!platform.Tenants.Any(item => item.Id == tenantId && item.Active)) return null;
            if (!platform.Users.Any(item => item.Id == credential.UserId && item.TenantId == tenantId && item.Active)) return null;
            return ValidateStoredSecret(credential.SecretKey, secretKey) ? credential : null;
        }

        /// <summary>
        /// Set a user's password.
        /// </summary>
        /// <param name="user">User.</param>
        /// <param name="password">Plaintext password supplied by the user.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="user"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="password"/> is empty.</exception>
        public void SetUserPassword(User user, string password)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            if (String.IsNullOrWhiteSpace(password)) throw new ArgumentException("Password is required.", nameof(password));

            user.PasswordSalt = GenerateToken();
            user.PasswordHash = HashPassword(user.PasswordSalt, password);
        }

        /// <summary>
        /// Validate a user's username/password pair.
        /// </summary>
        /// <param name="platform">Platform state.</param>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="username">Username or email address.</param>
        /// <param name="password">Plaintext password supplied by the user.</param>
        /// <returns>User when valid.</returns>
        public User? ValidateUserPassword(XenoPlatform platform, string tenantId, string username, string password)
        {
            if (platform == null) throw new ArgumentNullException(nameof(platform));
            if (String.IsNullOrWhiteSpace(username) || String.IsNullOrWhiteSpace(password)) return null;

            User? user = platform.Users.FirstOrDefault(item =>
                item.TenantId == tenantId &&
                item.Active &&
                (String.Equals(item.Username, username, StringComparison.OrdinalIgnoreCase) ||
                 String.Equals(item.Email, username, StringComparison.OrdinalIgnoreCase)));
            if (user == null) return null;
            if (!platform.Tenants.Any(item => item.Id == tenantId && item.Active)) return null;
            if (String.IsNullOrWhiteSpace(user.PasswordSalt) || String.IsNullOrWhiteSpace(user.PasswordHash)) return null;

            string candidateHash = HashPassword(user.PasswordSalt, password);
            return FixedTimeEquals(user.PasswordHash, candidateHash) ? user : null;
        }

        /// <summary>
        /// Revoke an auth session.
        /// </summary>
        /// <param name="platform">Platform state.</param>
        /// <param name="sessionId">Session ID.</param>
        /// <returns>True if a session was revoked.</returns>
        public bool RevokeSession(XenoPlatform platform, string? sessionId)
        {
            if (platform == null) throw new ArgumentNullException(nameof(platform));
            if (String.IsNullOrWhiteSpace(sessionId)) return false;

            AuthSession? session = platform.AuthSessions.FirstOrDefault(item => item.Id == sessionId && item.Active);
            if (session == null) return false;
            session.Active = false;
            session.LastUpdateUtc = DateTime.UtcNow;
            return true;
        }

        /// <summary>
        /// Resolve effective permissions for a request context.
        /// </summary>
        /// <param name="platform">Platform state.</param>
        /// <param name="context">Request context.</param>
        /// <returns>Effective permissions.</returns>
        public List<EffectivePermission> GetEffectivePermissions(XenoPlatform platform, RequestContext context)
        {
            if (platform == null) throw new ArgumentNullException(nameof(platform));
            if (context == null) throw new ArgumentNullException(nameof(context));

            User? user = String.IsNullOrWhiteSpace(context.UserId)
                ? null
                : platform.Users.FirstOrDefault(item => item.Id == context.UserId && item.TenantId == context.TenantId);

            if (context.PrincipalType == PrincipalTypeEnum.Administrator || (user != null && (user.IsAdmin || user.IsTenantAdmin)))
            {
                return new List<EffectivePermission>
                {
                    new EffectivePermission { ResourceType = "*", OperationType = OperationTypeEnum.Read, Permit = true, SourceRole = "TenantAdmin" },
                    new EffectivePermission { ResourceType = "*", OperationType = OperationTypeEnum.Write, Permit = true, SourceRole = "TenantAdmin" },
                    new EffectivePermission { ResourceType = "*", OperationType = OperationTypeEnum.Delete, Permit = true, SourceRole = "TenantAdmin" },
                    new EffectivePermission { ResourceType = "Admin", OperationType = OperationTypeEnum.Admin, Permit = true, SourceRole = "TenantAdmin" },
                    new EffectivePermission { ResourceType = "*", OperationType = OperationTypeEnum.Execute, Permit = true, SourceRole = "TenantAdmin" }
                };
            }

            string? principalId = context.PrincipalType == PrincipalTypeEnum.Credential ? context.CredentialId : context.UserId;
            if (String.IsNullOrWhiteSpace(principalId)) return new List<EffectivePermission>();

            List<RoleAssignment> assignments = platform.RoleAssignments
                .Where(item => item.TenantId == context.TenantId)
                .Where(item => item.Active)
                .Where(item => item.PrincipalType == context.PrincipalType)
                .Where(item => item.PrincipalId == principalId)
                .ToList();

            List<EffectivePermission> permissions = new List<EffectivePermission>();
            foreach (RoleAssignment assignment in assignments)
            {
                Role? role = platform.Roles.FirstOrDefault(item => item.Id == assignment.RoleId && item.TenantId == context.TenantId && item.Active);
                if (role == null) continue;
                foreach (string permissionId in role.PermissionIds)
                {
                    Permission? permission = platform.Permissions.FirstOrDefault(item => item.Id == permissionId && item.TenantId == context.TenantId && item.Active);
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
        /// Determine whether the request context has permission.
        /// </summary>
        /// <param name="platform">Platform state.</param>
        /// <param name="context">Request context.</param>
        /// <param name="resourceType">Resource type.</param>
        /// <param name="operationType">Operation type.</param>
        /// <returns>True if permitted.</returns>
        public bool IsAllowed(XenoPlatform platform, RequestContext context, string resourceType, OperationTypeEnum operationType)
        {
            List<EffectivePermission> permissions = GetEffectivePermissions(platform, context);
            EffectivePermission? explicitDeny = permissions.FirstOrDefault(item =>
                !item.Permit &&
                Matches(item.ResourceType, resourceType) &&
                item.OperationType == operationType);
            if (explicitDeny != null) return false;

            return permissions.Any(item =>
                item.Permit &&
                Matches(item.ResourceType, resourceType) &&
                (item.OperationType == operationType || (operationType == OperationTypeEnum.Read && item.OperationType == OperationTypeEnum.Admin)));
        }

        /// <summary>
        /// Append an audit record.
        /// </summary>
        /// <param name="platform">Platform state.</param>
        /// <param name="context">Request context.</param>
        /// <param name="resourceType">Resource type.</param>
        /// <param name="operationType">Operation type.</param>
        /// <param name="allowed">Whether access was allowed.</param>
        /// <param name="reasonCode">Reason code.</param>
        /// <param name="statusCode">HTTP status code.</param>
        /// <param name="targetTenantId">Optional target tenant ID.</param>
        /// <param name="targetResourceId">Optional target resource ID.</param>
        /// <returns>Audit record.</returns>
        public AuditRecord AppendAudit(
            XenoPlatform platform,
            RequestContext context,
            string resourceType,
            OperationTypeEnum operationType,
            bool allowed,
            string reasonCode,
            int statusCode,
            string? targetTenantId = null,
            string? targetResourceId = null)
        {
            if (platform == null) throw new ArgumentNullException(nameof(platform));
            if (context == null) throw new ArgumentNullException(nameof(context));

            AuditRecord record = new AuditRecord
            {
                TenantId = context.TenantId,
                TargetTenantId = targetTenantId,
                TargetResourceId = targetResourceId,
                RequestId = context.RequestId,
                PrincipalType = context.PrincipalType,
                PrincipalId = context.PrincipalId,
                AuthScheme = context.AuthScheme,
                ResourceType = resourceType,
                OperationType = operationType,
                Allowed = allowed,
                ReasonCode = reasonCode,
                SourceIp = context.SourceIp,
                StatusCode = statusCode
            };
            platform.AuditRecords.Add(record);
            return record;
        }

        /// <summary>
        /// Generate a new credential secret.
        /// </summary>
        /// <returns>Credential secret.</returns>
        public string GenerateSecret()
        {
            return GenerateToken().Substring(5);
        }

        /// <summary>
        /// Hash a token or secret.
        /// </summary>
        /// <param name="token">Token value.</param>
        /// <returns>Base64 SHA-256 hash.</returns>
        public string HashToken(string token)
        {
            if (token == null) throw new ArgumentNullException(nameof(token));
            byte[] bytes = Encoding.UTF8.GetBytes(token);
            byte[] hash = SHA256.HashData(bytes);
            return Convert.ToBase64String(hash);
        }

        /// <summary>
        /// Hash a credential secret for storage.
        /// </summary>
        /// <param name="secret">Plaintext secret.</param>
        /// <returns>Stored secret hash.</returns>
        public string HashCredentialSecret(string secret)
        {
            if (String.IsNullOrWhiteSpace(secret)) throw new ArgumentException("Secret is required.", nameof(secret));
            return "sha256:" + HashToken(secret);
        }

        private string HashPassword(string salt, string password)
        {
            if (String.IsNullOrWhiteSpace(salt)) throw new ArgumentException("Salt is required.", nameof(salt));
            if (password == null) throw new ArgumentNullException(nameof(password));
            return HashToken(salt + ":" + password);
        }

        private bool ValidateStoredSecret(string storedSecret, string suppliedSecret)
        {
            if (String.IsNullOrWhiteSpace(storedSecret) || String.IsNullOrWhiteSpace(suppliedSecret)) return false;
            if (storedSecret.StartsWith("sha256:", StringComparison.Ordinal))
            {
                return FixedTimeEquals(storedSecret, HashCredentialSecret(suppliedSecret));
            }

            return FixedTimeEquals(storedSecret, suppliedSecret);
        }

        private static bool Matches(string candidate, string resourceType)
        {
            return String.Equals(candidate, "*", StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(candidate, resourceType, StringComparison.OrdinalIgnoreCase);
        }

        private static bool FixedTimeEquals(string left, string right)
        {
            byte[] leftBytes = Encoding.UTF8.GetBytes(left);
            byte[] rightBytes = Encoding.UTF8.GetBytes(right);
            if (leftBytes.Length != rightBytes.Length) return false;
            return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
        }

        private static string GenerateToken()
        {
            byte[] bytes = RandomNumberGenerator.GetBytes(32);
            string token = Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
            return "xeno_" + token;
        }
    }
}

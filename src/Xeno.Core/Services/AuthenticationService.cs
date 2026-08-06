namespace Xeno.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Database;
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
        /// <param name="database">Database driver.</param>
        /// <param name="settings">Authentication settings.</param>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="principalType">Principal type.</param>
        /// <param name="userId">User ID.</param>
        /// <param name="credentialId">Credential ID.</param>
        /// <param name="authScheme">Authentication scheme.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Token issuance result.</returns>
        public async Task<AuthTokenResult> IssueTokenAsync(
            DatabaseDriverBase database,
            AuthSettings settings,
            string tenantId,
            PrincipalTypeEnum principalType,
            string? userId,
            string? credentialId,
            AuthSchemeEnum authScheme,
            CancellationToken token = default)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));

            string bearerToken = GenerateToken();
            DateTime expiresUtc = DateTime.UtcNow.AddMinutes(Math.Max(1, settings.SessionMinutes));
            AuthSession session = new AuthSession
            {
                TenantId = tenantId,
                PrincipalType = principalType,
                UserId = userId,
                CredentialId = credentialId,
                TokenHash = HashToken(bearerToken),
                AuthScheme = authScheme,
                ExpiresUtc = expiresUtc,
                Active = true
            };
            await database.AuthSessions!.CreateAsync(session, token).ConfigureAwait(false);

            return new AuthTokenResult
            {
                Token = bearerToken,
                TenantId = tenantId,
                SessionId = session.Id,
                ExpiresUtc = expiresUtc
            };
        }

        /// <summary>
        /// Validate a bearer token.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="bearerToken">Bearer token.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Active auth session.</returns>
        public async Task<AuthSession?> ValidateTokenAsync(DatabaseDriverBase database, string bearerToken, CancellationToken token = default)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (String.IsNullOrWhiteSpace(bearerToken)) return null;

            string tokenHash = HashToken(bearerToken);
            DateTime now = DateTime.UtcNow;
            AuthSession? session = await database.AuthSessions!.ReadByTokenHashAsync(tokenHash, token).ConfigureAwait(false);
            if (session == null) return null;
            if (!(session.Active && session.ExpiresUtc > now && String.Equals(session.TokenHash, tokenHash, StringComparison.Ordinal))) return null;
            if (!await IsSessionValidAsync(database, session, token).ConfigureAwait(false)) return null;
            return session;
        }

        /// <summary>
        /// Validate a bearer token for a specific tenant.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="bearerToken">Bearer token.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Active auth session.</returns>
        public async Task<AuthSession?> ValidateTokenAsync(DatabaseDriverBase database, string tenantId, string bearerToken, CancellationToken token = default)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (String.IsNullOrWhiteSpace(bearerToken)) return null;

            AuthSession? session = await ValidateTokenAsync(database, bearerToken, token).ConfigureAwait(false);
            if (session == null || !String.Equals(session.TenantId, tenantId, StringComparison.OrdinalIgnoreCase)) return null;
            return session;
        }

        /// <summary>
        /// Validate a direct credential by globally unique access key.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="accessKey">Access key.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Credential when valid and unambiguous.</returns>
        public async Task<Credential?> ValidateCredentialAsync(DatabaseDriverBase database, string accessKey, CancellationToken token = default)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (String.IsNullOrWhiteSpace(accessKey)) return null;

            List<Credential> candidates = await database.Credentials!.EnumerateByAccessKeyAsync(accessKey, token).ConfigureAwait(false);
            List<Credential> credentials = new List<Credential>();
            foreach (Credential candidate in candidates)
            {
                if (!candidate.Active) continue;
                if (!String.Equals(candidate.AccessKey, accessKey, StringComparison.Ordinal)) continue;
                if (!await IsCredentialValidAsync(database, candidate, token).ConfigureAwait(false)) continue;
                credentials.Add(candidate);
            }

            return credentials.Count == 1 ? credentials[0] : null;
        }

        /// <summary>
        /// Validate a direct credential for a specific tenant.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="accessKey">Access key.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Credential when valid.</returns>
        public async Task<Credential?> ValidateCredentialAsync(DatabaseDriverBase database, string tenantId, string accessKey, CancellationToken token = default)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (String.IsNullOrWhiteSpace(accessKey)) return null;

            Credential? credential = await database.Credentials!.ReadByAccessKeyAsync(tenantId, accessKey, token).ConfigureAwait(false);
            if (credential == null || !credential.Active) return null;
            if (!String.Equals(credential.AccessKey, accessKey, StringComparison.Ordinal)) return null;

            Tenant? tenant = await database.Tenants!.ReadAsync(tenantId, token).ConfigureAwait(false);
            if (tenant == null || !tenant.Active) return null;

            User? user = await database.Users!.ReadAsync(tenantId, credential.UserId, token).ConfigureAwait(false);
            if (user == null || !user.Active) return null;
            return credential;
        }

        private static async Task<bool> IsSessionValidAsync(DatabaseDriverBase database, AuthSession session, CancellationToken token)
        {
            Tenant? tenant = await database.Tenants!.ReadAsync(session.TenantId, token).ConfigureAwait(false);
            if (tenant == null || !tenant.Active) return false;

            if (!String.IsNullOrWhiteSpace(session.UserId))
            {
                User? user = await database.Users!.ReadAsync(session.TenantId, session.UserId!, token).ConfigureAwait(false);
                if (user == null || !user.Active) return false;
            }

            if (!String.IsNullOrWhiteSpace(session.CredentialId))
            {
                Credential? credential = await database.Credentials!.ReadAsync(session.TenantId, session.CredentialId!, token).ConfigureAwait(false);
                if (credential == null || !credential.Active) return false;
            }

            return true;
        }

        private static async Task<bool> IsCredentialValidAsync(DatabaseDriverBase database, Credential credential, CancellationToken token)
        {
            Tenant? tenant = await database.Tenants!.ReadAsync(credential.TenantId, token).ConfigureAwait(false);
            if (tenant == null || !tenant.Active) return false;

            User? user = await database.Users!.ReadAsync(credential.TenantId, credential.UserId, token).ConfigureAwait(false);
            return user != null && user.Active;
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
        /// <param name="database">Database driver.</param>
        /// <param name="tenantId">Tenant ID.</param>
        /// <param name="username">Username or email address.</param>
        /// <param name="password">Plaintext password supplied by the user.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>User when valid.</returns>
        public async Task<User?> ValidateUserPasswordAsync(DatabaseDriverBase database, string tenantId, string username, string password, CancellationToken token = default)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (String.IsNullOrWhiteSpace(username) || String.IsNullOrWhiteSpace(password)) return null;

            User? user = await database.Users!.ReadByUsernameAsync(tenantId, username, token).ConfigureAwait(false);
            if (user == null)
            {
                user = await database.Users!.ReadByEmailAsync(tenantId, username, token).ConfigureAwait(false);
            }

            if (user == null || !user.Active) return null;

            Tenant? tenant = await database.Tenants!.ReadAsync(tenantId, token).ConfigureAwait(false);
            if (tenant == null || !tenant.Active) return null;
            if (String.IsNullOrWhiteSpace(user.PasswordSalt) || String.IsNullOrWhiteSpace(user.PasswordHash)) return null;

            string candidateHash = HashPassword(user.PasswordSalt, password);
            return FixedTimeEquals(user.PasswordHash, candidateHash) ? user : null;
        }

        /// <summary>
        /// Revoke an auth session.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="sessionId">Session ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if a session was revoked.</returns>
        public async Task<bool> RevokeSessionAsync(DatabaseDriverBase database, string? sessionId, CancellationToken token = default)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (String.IsNullOrWhiteSpace(sessionId)) return false;

            AuthSession? session = await database.AuthSessions!.ReadByIdAsync(sessionId, token).ConfigureAwait(false);
            if (session == null || !session.Active) return false;
            session.Active = false;
            session.LastUpdateUtc = DateTime.UtcNow;
            await database.AuthSessions!.UpdateAsync(session, token).ConfigureAwait(false);
            return true;
        }

        /// <summary>
        /// Resolve effective permissions for a request context.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="context">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Effective permissions.</returns>
        public async Task<List<EffectivePermission>> GetEffectivePermissionsAsync(DatabaseDriverBase database, RequestContext context, CancellationToken token = default)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (context == null) throw new ArgumentNullException(nameof(context));

            User? user = String.IsNullOrWhiteSpace(context.UserId)
                ? null
                : await database.Users!.ReadAsync(context.TenantId, context.UserId!, token).ConfigureAwait(false);

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

            List<RoleAssignment> allAssignments = await database.RoleAssignments!.EnumerateByPrincipalAsync(context.TenantId, principalId, token).ConfigureAwait(false);
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
                Role? role = await database.Roles!.ReadAsync(context.TenantId, assignment.RoleId, token).ConfigureAwait(false);
                if (role == null || !role.Active) continue;
                foreach (string permissionId in role.PermissionIds)
                {
                    Permission? permission = await database.Permissions!.ReadAsync(context.TenantId, permissionId, token).ConfigureAwait(false);
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
        /// Determine whether the request context has permission.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="context">Request context.</param>
        /// <param name="resourceType">Resource type.</param>
        /// <param name="operationType">Operation type.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if permitted.</returns>
        public async Task<bool> IsAllowedAsync(DatabaseDriverBase database, RequestContext context, string resourceType, OperationTypeEnum operationType, CancellationToken token = default)
        {
            List<EffectivePermission> permissions = await GetEffectivePermissionsAsync(database, context, token).ConfigureAwait(false);
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
        /// Construct an audit record for the supplied request context and outcome. Audit records are
        /// persisted to the dedicated audit table by the caller and are never cached in the in-memory
        /// platform state.
        /// </summary>
        /// <param name="database">Database driver. Unused; retained for call-site compatibility.</param>
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
            DatabaseDriverBase database,
            RequestContext context,
            string resourceType,
            OperationTypeEnum operationType,
            bool allowed,
            string reasonCode,
            int statusCode,
            string? targetTenantId = null,
            string? targetResourceId = null)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
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
                ForwardedFor = context.ForwardedFor,
                UserAgent = context.UserAgent,
                Origin = context.Origin,
                Referer = context.Referer,
                Host = context.Host,
                RequestMethod = context.RequestMethod,
                RequestPath = context.RequestPath,
                CorrelationId = context.CorrelationId,
                StatusCode = statusCode
            };

            if (allowed && (operationType == OperationTypeEnum.Write || operationType == OperationTypeEnum.Delete))
            {
                Telemetry.XenoTelemetry.RecordMutation(resourceType, operationType.ToString().ToLowerInvariant());
            }

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

# Xeno Cloud Onboarding (Verify + Approve)

Plan and working notes for gating public sign-ups to Xeno Cloud with an
email-verification + admin-approval flow, plus the marketing-site changes that
route prospects into it. Executed and tracked here; each actionable item is a
checkbox.

## Desired experience (from product owner)

1. User visits `app.xenocloud.ai/onboarding` and supplies **organization name,
   name, email, and completes a captcha**.
2. The webapp creates the tenant and user, marking the **tenant inactive** so
   they cannot log in.
3. User is diverted to a "Click the link in your email to verify" page.
4. The `Tenant` gains a **`CreationIpAddress`** property, set during signup
   (on-server-start schema migration across all supported providers).
5. The verification link returns the user to `app.xenocloud.ai/{…}` with a
   "Thanks for signing up! We'll notify you when your account is approved."
6. A **new approval email** is sent when the tenant is approved. Admins get a
   dashboard **Onboard** action on the Tenants page for inactive tenants that
   activates the tenant + user and sends the approval email.

Website (`c:\code\xeno\xenocloud.github.io`): see Workstream F.

> **Backend status (2026-08-04):** Workstreams A (storage/migration + settings), B (signup/verify/approve endpoints), and C (email templates) are code-complete and build warning-free. Deviations: (1) the /onboarding form collects a **password** in addition to org/name/email — without one the approved user cannot sign in; a set-password-on-approval link is the recommended alternative. (2) Captcha is client-side only for now; server-side Turnstile verification is stubbed via Onboarding.CaptchaSecretKey but not enforced. (3) OpenAPI docs for verify/approve and onboarding-row deletion on tenant nuke are follow-ups. Remaining: Workstream D (tests), E (dashboard), F (website).

## Verified facts (checked against the code, 2026-08-04)

- **Inactive tenant already blocks login.** `AuthenticationService.ValidateUserPassword`
  returns null when the tenant is not active (`...Tenants.Any(t => t.Id == tenantId && t.Active)`),
  and `ValidateSession` / `ValidateCredential` reject inactive tenants too. So
  marking the tenant inactive satisfies requirement #2 with no auth changes.
- **Tenants and users live in a JSON state blob**, not relational tables. The
  platform (`XenoPlatform.ToState()` → `PlatformState`) is serialized into the
  single-row `xeno_platform_state` table. Consequences:
  - Adding `Tenant.CreationIpAddress` (and any user verification fields) is a
    **blob property** — it auto-migrates (old blobs deserialize with defaults);
    there is no per-column DDL for it.
  - The genuine cross-provider **schema migration** belongs to the new
    `onboarding_requests` table (below), which holds verification tokens and
    onboarding status out of the hot platform blob. This mirrors the
    request-history and audit table decouples.
- **Signup endpoint is currently open**: `POST /v1.0/api/onboarding/signup`
  (`HandleOnboardingSignupAsync`) creates a tenant with `Active = true` and issues
  a token. This is what we gate.
- **Email infra is ready**: `EmailTemplateBase.RenderLayout(title, subject,
  preheader, bodyHtml, bodyText, ctaText?, ctaUrl?)` renders a themed layout with
  a CTA button. `IXenoEmailService.SendAsync(EmailMessage)` sends one message.
  `EmailSettings.ApplicationBaseUrl` (default `https://app.xenocloud.ai`) is the
  base for verification/approval links.

## Architecture decisions

- **Two signup modes stay**: create-tenant (no `tenantId`) and join-tenant
  (`tenantId` supplied). This plan gates the **create-tenant** path into the
  verify+approve flow. Join-tenant is out of scope for this pass (note it).
- **New onboarding record** persisted in `onboarding_requests` (relational,
  migrated) keyed by a random verification token. Status:
  `PendingVerification → Verified → Approved` (plus `Rejected` for future use).
- **No token/session on signup.** Signup returns only enough for the webapp to
  show the "check your email" page. Login is impossible until approval flips the
  tenant + user active.
- **User is created inactive too** (belt and suspenders); approval activates
  both tenant and user.
- **Captcha**: the webapp renders the widget (Cloudflare Turnstile recommended).
  The server optionally verifies the captcha token when a Turnstile secret is
  configured; when unset, captcha is enforced client-side only. Config lives in a
  new `Onboarding` settings block.

## Data model

`Tenant` (blob): add `CreationIpAddress` (string?, default null).

`onboarding_requests` table (all four providers, `CREATE TABLE IF NOT EXISTS` on
startup):

| column | notes |
| --- | --- |
| id | PK |
| tenant_id | created tenant |
| user_id | created admin user |
| organization | org/tenant name |
| contact_name | person's name |
| email | contact email (also lookup) |
| creation_ip | source IP at signup |
| token | random verification token (unique) |
| status | PendingVerification / Verified / Approved / Rejected |
| created_utc / verified_utc / approved_utc | timestamps |

`IOnboardingMethods`: `InsertAsync`, `GetByTokenAsync`, `GetByTenantAsync`,
`MarkVerifiedAsync(token)`, `MarkApprovedAsync(tenantId)`, `DeleteByTenantAsync`
(for nuke), `QueryAsync`/`CountAsync` (pending list for admins).

## Settings (`Onboarding` block, new)

- `Mode`: `Disabled | Open | VerifyAndApprove` (default `VerifyAndApprove`).
- `VerificationLinkBaseUrl`: defaults to `EmailSettings.ApplicationBaseUrl`.
- `Captcha`: `{ Provider: none|turnstile, SecretKey: string? }` (sensitive).
- Environment overrides for each, following the `ApplyEnvironmentSettings` pattern.

## Endpoints

- `POST /v1.0/api/onboarding/signup` — reworked create-tenant path:
  validate → captcha (optional) → create inactive tenant (`CreationIpAddress`) +
  inactive admin user → insert `onboarding_requests` (token) → send verification
  email → return `{ status: "pending_verification", email }`. No token issued.
- `POST /v1.0/api/onboarding/verify` (body `{ token }`) or
  `GET /v1.0/api/onboarding/verify?token=` — mark record `Verified`, stamp
  `verified_utc`, optionally alert platform admins. Public (no auth).
- `POST /v1.0/api/onboarding/approve` (admin, body `{ tenantId }`) — activate
  tenant + user, mark record `Approved`, send approval email. Requires global
  admin.
- `GET /v1.0/api/onboarding/pending` (admin) — list pending/verified onboarding
  records for the Tenants "Onboard" surface (optional; dashboard can also read
  the inactive tenants directly).

## Email templates (new)

- `OnboardingVerificationEmailTemplate` (+ context: org, name, verify URL) — CTA
  "Verify email" → `{base}/onboarding/verify?token={token}`.
- `TenantApprovedEmailTemplate` (+ context: org, name, sign-in URL) — CTA
  "Sign in to Xeno" → `{base}/`.
- Wire sends via `IXenoEmailService.SendAsync`; best-effort, never fail the
  request. Respect `EmailSettings.Enabled` (when disabled, log and skip — the
  admin can still approve, but no email goes out).

---

## Workstream A — Backend storage and migration

- [x] `Tenant.CreationIpAddress` (blob property), set on signup.
- [x] `Onboarding/OnboardingRequest.cs` model + `OnboardingStatusEnum`.
- [x] `IOnboardingMethods.cs`, `OnboardingQuery.cs`, `OnboardingRecordPage.cs`.
- [x] Four providers `*OnboardingMethods.cs` with `InitializeAsync` DDL (indexes
  on token, tenant_id, status) mirroring the audit table.
- [x] `DatabaseDriverBase.Onboarding` property + each driver `InitializeStorageAsync`
  wiring (create table + set property).
- [x] `Onboarding` settings block + `ApplyEnvironmentSettings` overrides.

## Workstream B — Signup / verify / approve

- [x] Rework `HandleOnboardingSignupAsync` create-tenant path: inactive tenant +
  user, `CreationIpAddress`, insert onboarding row, send verification email,
  return pending response (no token).
- [x] `HandleOnboardingVerifyAsync` (public) — mark verified.
- [x] `HandleOnboardingApproveAsync` (global admin) — activate tenant + user,
  mark approved, send approval email. Register routes + OpenAPI.
- [ ] Optional captcha verification helper (Turnstile) gated by settings.
- [ ] Onboarding rows deleted on tenant nuke (`DeleteByTenantAsync`).

## Workstream C — Email templates

- [x] `OnboardingVerificationEmailTemplate` + context, CTA verify link.
- [x] `TenantApprovedEmailTemplate` + context, CTA sign-in link.
- [x] Send helpers in `XenoServer` (best-effort), building URLs from
  `Onboarding.VerificationLinkBaseUrl` / `EmailSettings.ApplicationBaseUrl`.

## Workstream D — Tests

- [ ] Touchstone: signup creates inactive tenant + user + onboarding row; login
  is refused for the inactive tenant (positive/negative).
- [ ] Verify flips status to Verified; approve flips tenant/user active and status
  to Approved; unknown/used token is rejected.
- [ ] SQLite `onboarding_requests` round-trip (insert/get-by-token/mark/delete)
  and provider-init creates the table.

## Workstream E — Dashboard

- [ ] `/onboarding` page: organization, name, email, captcha widget; POST signup;
  on success show "check your email".
- [ ] `/onboarding/verify` landing: reads `?token`, POSTs verify, shows "Thanks
  for signing up! We'll notify you when your account is approved."
- [ ] Tenants page: **Onboard** row action visible for inactive tenants → calls
  approve → toast + refresh.
- [ ] Routing (public `/onboarding`, `/onboarding/verify`), i18n keys, build,
  `verify-i18n`.

## Workstream F — Marketing website (`xenocloud.github.io`)

- [ ] Hero: "Start with Docker" → **"Deploy On-Premises"**; add **"Cloud Early
  Access"** button linking to `app.xenocloud.ai/onboarding`.
- [ ] "How it works": cover on-prem + SaaS ("Deploy securely in your environment,
  or, use our SaaS platform." + the provided subtext). Move the DEPLOYMENT
  OPTIONS images beneath the 01–04 cards; remove the DEPLOYMENT OPTIONS header
  and its text.
- [ ] "GETTING STARTED" → **"On-Premises Quickstart"**.
- [ ] Add **"Cloud Quickstart"** section (global-network benefits + "Cloud Early
  Access" CTA).
- [ ] Header + footer **"Cloud Early Access"** buttons.
- [ ] Header: GitHub icon (top far right) + light/dark toggle.
- [ ] Footer: GitHub icon.

## Open questions / follow-ups

- Captcha provider + keys (Turnstile vs hCaptcha) — webapp needs the site key;
  server needs the secret. Confirm choice.
- Join-tenant (invite to an existing org) gating — deferred.
- Rate-limiting the public signup endpoint per IP — recommended add-on.

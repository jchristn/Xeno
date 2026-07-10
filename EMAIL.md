# Xeno Email Messaging Plan

Xeno needs email for administrative and human-in-the-loop workflows, not for request routing. The first version should be small, provider-agnostic, testable without live credentials, and wired only at product moments where the server already knows the actor, tenant, and outcome. Onboarding is the natural first integration point: it already creates tenants, users, credentials, role assignments, audit records, and a response DTO in one server path.

The implementation should add a new `Xeno.Email` project and keep application code away from Mailgun or any future provider SDK. `Xeno.Server` should depend on an `IXenoEmailService` abstraction and submit either a rendered `EmailMessage` or an intent-specific notification request. Provider classes translate that neutral message into Mailgun, SMTP, a file sink, or a disabled no-op.

## Goals

- [ ] Add email delivery plumbing that can be enabled from server JSON configuration.
- [ ] Keep provider-specific code under `Xeno.Email.Providers`.
- [ ] Keep reusable layout and message body construction under `Xeno.Email.Templates`.
- [ ] Support Mailgun first through `SendWithMailgun`, without leaking Mailgun types into server workflows.
- [ ] Support disabled and test providers so local development and automated tests do not send real email.
- [ ] Wire the first production notifications to onboarding events.
- [ ] Document configuration, REST/dashboard behavior, SDK expectations, Postman examples, and test coverage.

## Non-Goals For The First Pass

- [ ] Do not add a user notification preference system yet.
- [ ] Do not add unsubscribe, marketing email, or bulk campaign support.
- [ ] Do not create a database-backed email queue in the first pass.
- [ ] Do not expose a public endpoint that sends arbitrary email content.
- [ ] Do not email one-time credential secret keys unless there is an explicit product decision to do so. The onboarding response already returns one-time-visible secrets, and email is not the right default transport for them.

## Project Shape

Add `src/Xeno.Email/Xeno.Email.csproj` to `src/Xeno.sln`.

`Xeno.Email` should target `net10.0`, disable implicit usings, enable nullable reference types, generate XML documentation, and reference `Xeno.Core`. The project may reference `SendWithMailgun` for the Mailgun provider. Keep the package version out of this plan; choose and pin the current stable version during implementation.

Recommended namespaces and files:

```text
src/Xeno.Email/
  Xeno.Email.csproj
  EmailAddress.cs
  EmailAttachment.cs
  EmailContent.cs
  EmailMessage.cs
  EmailSendResult.cs
  EmailDeliveryException.cs
  EmailProviderTypeEnum.cs
  IEmailProvider.cs
  IEmailTemplate.cs
  IXenoEmailService.cs
  XenoEmailService.cs
  Providers/
    DisabledEmailProvider.cs
    FileEmailProvider.cs
    MailgunEmailProvider.cs
    EmailProviderFactory.cs
  Templates/
    EmailTemplateBase.cs
    EmailTemplateRenderResult.cs
    EmailTemplateTheme.cs
    OnboardingAdminAlertEmailTemplate.cs
    TenantUserJoinedEmailTemplate.cs
    TenantCreatedWelcomeEmailTemplate.cs
    TokenBalanceFloorApproachingEmailTemplate.cs
    EmailHtmlEncoder.cs
    EmailRedactionHelpers.cs
  Workflows/
    OnboardingEmailWorkflow.cs
    OnboardingEmailContext.cs
```

`Xeno.Email` should own the provider abstraction and templates. `Xeno.Core` should own only settings DTOs because `XenoSettings` already lives there. Avoid making `Xeno.Core` reference `Xeno.Email`; that would create the wrong dependency direction.

## Configuration

Add an `Email` property to `Xeno.Core.Settings.XenoSettings`.

Recommended DTOs in `src/Xeno.Core/Settings`:

```text
EmailSettings.cs
MailgunEmailSettings.cs
EmailNotificationSettings.cs
EmailRecipientSettings.cs
```

Suggested JSON shape:

```json
{
  "Email": {
    "Enabled": false,
    "Provider": "Disabled",
    "FromAddress": "noreply@xenocloud.ai",
    "FromName": "Xeno",
    "ReplyToAddress": "support@xenocloud.ai",
    "SupportAddress": "support@xenocloud.ai",
    "ApplicationBaseUrl": "https://app.xenocloud.ai",
    "SubjectPrefix": "",
    "MaxBodyBytes": 262144,
    "Mailgun": {
      "Domain": "",
      "ApiKey": "",
      "Region": "US"
    },
    "File": {
      "Directory": "data/email-outbox"
    },
    "Notifications": {
      "NotifyPlatformAdministratorsOnTenantCreated": true,
      "NotifyPlatformAdministratorsOnUserJoined": true,
      "NotifyTenantAdministratorsOnUserJoined": true,
      "NotifyNewTenantAdministrator": true,
      "NotifyNewTenantMember": true,
      "PlatformAdministratorRecipients": [
        {
          "Email": "admin@xeno",
          "Name": "Xeno Admin"
        }
      ]
    }
  }
}
```

Provider values should be:

- `Disabled`: default; accepts send requests and returns a skipped result.
- `File`: writes rendered messages to disk for local development and tests.
- `Mailgun`: sends through `Xeno.Email.Providers.MailgunEmailProvider`.

Secrets belong in the server settings file for now because that is how Xeno currently handles database and auth secrets. Document environment-specific overrides as a follow-up if Xeno later adopts a configuration provider stack. The dashboard must treat provider secrets like database passwords: editable, saved intentionally, and not logged.

`Xeno.Server.ApplySettings` must copy `incoming.Email ?? new EmailSettings()`, and the raw settings editor/dashboard normalizer must preserve the `Email` object. Runtime changes to recipient lists, subject prefix, and notification toggles can be live. Provider type, Mailgun domain/API key, and file sink directory should be marked restart-required unless the server explicitly rebuilds the email provider after settings save.

## Provider Boundary

Application code should never construct a Mailgun sender. The only public provider surface needed by the rest of the product is:

```csharp
public interface IEmailProvider
{
    Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken token = default);
}
```

`EmailMessage` should include:

- `From`: `EmailAddress`
- `ReplyTo`: optional `EmailAddress`
- `To`, `Cc`, `Bcc`: `List<EmailAddress>`
- `Subject`: string
- `HtmlBody`: string
- `TextBody`: string
- `Tags`: `Dictionary<string, string>`
- `CorrelationId`: optional string
- `TemplateId`: optional string

`EmailSendResult` should include:

- `Accepted`: bool
- `Skipped`: bool
- `Provider`: string
- `ProviderMessageId`: string
- `StatusMessage`: string
- `SentUtc`: `DateTime`

Provider implementations:

- [ ] `DisabledEmailProvider`: returns `Skipped = true` and does not log bodies.
- [ ] `FileEmailProvider`: writes `.json`, `.html`, and `.txt` files to the configured outbox directory; useful for tests and local inspection.
- [ ] `MailgunEmailProvider`: validates `Domain`, `ApiKey`, sender, and recipients; maps neutral message fields into `SendWithMailgun`; catches provider exceptions and throws `EmailDeliveryException` with sanitized context.

Logging must include provider, subject, recipient count, template ID, correlation ID, and provider message ID. Do not log bodies, API keys, authorization tokens, credential secret keys, passwords, or full request bodies.

## Service Boundary

`IXenoEmailService` should sit above providers and templates. It is the surface `Xeno.Server` uses.

Recommended methods:

```csharp
Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken token = default);
Task<IReadOnlyList<EmailSendResult>> SendOnboardingNotificationsAsync(OnboardingEmailContext context, CancellationToken token = default);
Task<EmailTemplateRenderResult> RenderTemplateAsync<TContext>(IEmailTemplate<TContext> template, TContext context, CancellationToken token = default);
```

The onboarding workflow can be implemented in `Xeno.Email.Workflows.OnboardingEmailWorkflow`, with `XenoEmailService` delegating to it. That keeps the server route short and makes notification fan-out testable without a Watson server.

Do not model the service like the Jurat sample's large `IEmailService` with one method per possible notification. Xeno should keep intent-specific methods only where they protect product logic. For generic system events, the server should render a template and call `SendAsync`.

## Templates

Templates should produce both HTML and plain text. The HTML should be simple table-safe email markup, not dashboard CSS. The base template owns the header, footer, typography, support link, optional CTA button, and product identity.

Template base responsibilities:

- [ ] Encode all user-controlled values.
- [ ] Provide `RenderLayout(title, preheader, bodyHtml, bodyText, cta)` or equivalent.
- [ ] Include support/reply-to copy from `EmailSettings`.
- [ ] Include UTC timestamps and host/environment metadata only for administrative emails.
- [ ] Bound rendered body length using `EmailSettings.MaxBodyBytes`.
- [ ] Redact sensitive names such as `password`, `secretKey`, `token`, `authorization`, `apiKey`, `credential`, `x-token`, and `x-access-key` when diagnostic JSON is included.

Initial templates:

- [ ] `OnboardingAdminAlertEmailTemplate`: sent to platform administrators when a tenant or user is created through onboarding.
- [ ] `TenantUserJoinedEmailTemplate`: sent to tenant administrators when a non-admin user joins their tenant.
- [ ] `TenantCreatedWelcomeEmailTemplate`: sent to the first tenant administrator after a new tenant is created.
- [ ] `TokenBalanceFloorApproachingEmailTemplate`: implemented but not wired until token balances exist. It establishes the future shape without inventing token-balance persistence now.

Each template should have a stable `TemplateId`, for example `onboarding.admin-alert.v1`. Keep the version in the ID so future copy changes can be tracked without guessing which body a recipient saw.

## Initial Communications

| Communication | Trigger | Recipients | Why it belongs here | First-pass behavior |
| --- | --- | --- | --- | --- |
| Platform admin tenant-created alert | `POST /v1.0/api/onboarding/signup` when `tenantId` is omitted | `Email.Notifications.PlatformAdministratorRecipients` | The platform owner needs to know new companies are appearing. | Send after platform save succeeds; failure to send must not roll back onboarding. |
| Platform admin user-joined alert | `POST /v1.0/api/onboarding/signup` when `tenantId` is supplied | `Email.Notifications.PlatformAdministratorRecipients` | Human operators may want to review unusual growth or support new users. | Send after platform save succeeds; include tenant/user IDs and source IP, no password/secret/token. |
| Tenant admin user-joined alert | `POST /v1.0/api/onboarding/signup` when a non-admin user joins an existing tenant | Active tenant admins in that tenant | A company administrator should know someone joined their org. | Send to active tenant admins other than the new user. If no tenant admin exists, skip and log a warning. |
| New tenant administrator welcome | `POST /v1.0/api/onboarding/signup` when a new tenant is created | New tenant admin email | The person who created the company should receive confirmation and next steps. | Send a product email with tenant ID, dashboard link, and support link. Do not include password, token, access key, or secret key. |
| New tenant member welcome | `POST /v1.0/api/onboarding/signup` when joining an existing tenant | New user email | The new user needs confirmation that their account exists. | Optional in first pass behind `NotifyNewTenantMember`; keep copy terse and non-sensitive. |
| Token balance floor warning | Future token balance service | User or tenant billing/usage contacts | The user specifically named this future workflow. | Template and DTO only; no server trigger until balance data exists. |

Email must be best-effort for onboarding. The user/company creation has already succeeded by the time notifications are sent. If email fails, append an audit record or log entry with sanitized details, return the onboarding success response, and expose the send failure only through server logs/metrics in the first pass.

## Backend Integration

### Solution And References

- [ ] Create `src/Xeno.Email/Xeno.Email.csproj`.
- [ ] Add `Xeno.Email` to `src/Xeno.sln`.
- [ ] Add a project reference from `src/Xeno.Server/Xeno.Server.csproj` to `..\Xeno.Email\Xeno.Email.csproj`.
- [ ] Add `SendWithMailgun` to `Xeno.Email.csproj`.
- [ ] Keep `Xeno.Core` free of provider package references.

### Settings

- [ ] Add `EmailSettings` and nested settings classes under `src/Xeno.Core/Settings`.
- [ ] Add `public EmailSettings Email { get; set; } = new EmailSettings();` to `XenoSettings`.
- [ ] Update `XenoServer.ApplySettings` to preserve the `Email` section.
- [ ] Update `xeno.json`, `docker/xeno.json`, and `docker/factory/xeno.json` with disabled email defaults.
- [ ] Update dashboard defaults and normalization so `Email` is never dropped when saving settings.

### Server Lifetime

- [ ] Add private fields to `XenoServer` for `IXenoEmailService` or an `IEmailProvider`.
- [ ] Build the email service in the `XenoServer` constructor or `StartAsync` from `_Settings.Email`.
- [ ] Rebuild the service after settings save only if live reload is explicitly implemented. Otherwise mark provider changes restart-required.
- [ ] Dispose providers that hold disposable clients or file handles.

### Onboarding Route

`HandleOnboardingSignupAsync` should create a local `AuthOnboardingResponse` variable before sending the HTTP response. After `SavePlatformAsync(token)` succeeds and before or after `SendJsonAsync`, call the onboarding email workflow with:

- created tenant details
- created user details
- whether a new tenant was created
- active tenant administrator recipients
- platform administrator recipients from settings
- source IP
- request correlation ID if one exists
- current UTC timestamp

Prefer sending notifications after the response is assembled but before it is written only if the provider has a very short timeout and failures are swallowed. A safer first pass is to send after the platform save and before response write with a bounded timeout of a few seconds. If send latency becomes visible, move to a background queue.

No email should include:

- password
- bearer token
- access key
- secret key
- hashed secret
- full request body

### Audit And Metrics

- [ ] Add audit records or structured logs for attempted email sends.
- [ ] Add Prometheus-style metrics in `/metrics`: sent count, skipped count, failed count, and last failure timestamp by provider/template.
- [ ] Keep labels low-cardinality: provider and template ID are okay; recipient email is not.

### Failure Behavior

- [ ] Provider configuration invalid at startup should not crash the server when `Email.Enabled` is false.
- [ ] Provider configuration invalid at startup should fail fast when `Email.Enabled` is true and `Provider` is `Mailgun`.
- [ ] Send failure during onboarding should log and audit a notification failure but must not undo tenant/user creation.
- [ ] Cancellation should be honored. Every async email method must accept `CancellationToken` and use `ConfigureAwait(false)`.

## REST API

No public "send email" endpoint should be added in the first pass.

Existing settings routes already expose and persist the server settings:

- `GET /v1.0/api/settings`
- `POST /v1.0/api/settings`

REST/OpenAPI work:

- [ ] Include `Email` in `XenoSettingsBodySchema()` and the `updateSettings` example.
- [ ] Include an OpenAPI schema component for the `Email` settings shape if the current manual schema builder supports it cleanly.
- [ ] Update `REST_API.md` under Settings with the `Email` section, provider values, secret handling, and restart behavior.
- [ ] Document that onboarding can trigger notification email when enabled but that onboarding response shape does not change.

Avoid changing onboarding response contracts for email send status. Email is operational side effect, not part of caller-visible account creation semantics.

## Dashboard

The server dashboard already has a settings page. Extend that page rather than adding a new navigation item.

Dashboard work:

- [ ] Add `Email` defaults in `defaultServerSettings()`.
- [ ] Update `normalizeSettings()` to preserve `Email`, `Email.Mailgun`, `Email.File`, and `Email.Notifications`.
- [ ] Add an "Email Settings" panel near Logging/Database.
- [ ] Controls:
  - provider selector: `Disabled`, `File`, `Mailgun`
  - enabled toggle
  - from address/name
  - reply-to/support address
  - application base URL
  - subject prefix
  - Mailgun domain
  - Mailgun API key password input
  - file outbox directory
  - notification toggles
  - platform administrator recipient textarea or editable list
- [ ] Add i18n resource keys for labels and tooltips.
- [ ] Show restart impact for provider/domain/API key/file directory changes unless live rebuild is implemented.
- [ ] Do not render the API key in summaries, raw diagnostic views, or toast messages.

No frontend notification center is needed yet. Email events are server operational events, and the dashboard's job in the first pass is configuration.

## SDKs

No SDK should gain a `SendEmail` method in the first pass.

SDK work:

- [ ] Confirm C#, JavaScript, and Python settings update helpers preserve unknown nested settings, including `Email`.
- [ ] Add typed C# settings models only if the SDK moves away from `JsonElement` for settings. Today `SettingsResponse.Settings` is already generic.
- [ ] Update SDK README coverage statements to mention email settings only as part of server settings management.
- [ ] Add SDK tests around settings payload passthrough if any SDK currently normalizes or filters settings.

Onboarding SDK methods should not change. A caller calling `OnboardAsync` may cause server email as a side effect, but the SDK does not need to know.

## Postman

Postman should show configuration but not expose a send-email route.

- [ ] Update `Xeno.postman_collection.json` settings update example to include a disabled `Email` section.
- [ ] Add a "Server Settings - Email Disabled" example under Settings.
- [ ] Add a "Server Settings - Mailgun Template" example with placeholder values such as `YOUR_MAILGUN_DOMAIN` and `YOUR_MAILGUN_API_KEY`.
- [ ] Keep live secrets out of the collection.

## Documentation

Documentation updates:

- [ ] `README.md`: configuration section should mention server email settings and Mailgun support.
- [ ] `REST_API.md`: settings section should document `Email` configuration and onboarding notification side effects.
- [ ] `SECURITY_BEST_PRACTICES.md`: add email secret handling, redaction, and warning against emailing credentials/tokens.
- [ ] `CHANGELOG.md`: add an unreleased entry when implementation lands.
- [ ] `getting-started/*_SERVER.md`: add optional email provider configuration notes only if those guides already discuss production configuration.
- [ ] `CLIENT_GETTING_STARTED.md`: no change unless it mentions server settings broadly.

Suggested README snippet:

```markdown
Server email notifications are disabled by default. Set `Email.Enabled` to `true`, choose `Email.Provider`, and configure the provider-specific section such as `Email.Mailgun` to send onboarding and administrative notifications. Xeno never includes passwords, bearer tokens, access keys, or secret keys in notification email.
```

## Tests

Unit tests belong in the new email project or the existing shared test harness. Because the current solution uses `Test.Shared` through `Test.Automated`, `Test.Xunit`, and `Test.Nunit`, shared behavioral tests are preferred for server-visible behavior.

### Email Project Tests

- [ ] Template rendering encodes HTML in tenant names, usernames, email addresses, and source IP values.
- [ ] Plain text and HTML bodies are both present.
- [ ] Sensitive fields are redacted from diagnostic JSON.
- [ ] `DisabledEmailProvider` returns skipped without throwing.
- [ ] `FileEmailProvider` writes deterministic files and redacts unsafe content in metadata.
- [ ] `MailgunEmailProvider` maps from neutral `EmailMessage` to provider call through a seam that can be faked. Do not require live Mailgun credentials in automated tests.
- [ ] Provider factory rejects invalid enabled Mailgun settings with a specific exception.

### Server Tests

- [ ] `XenoSettings` serialization includes the `Email` section with disabled defaults.
- [ ] Settings update persists `Email` and does not drop existing nested sections.
- [ ] Onboarding new tenant triggers platform admin alert and new tenant admin welcome when enabled.
- [ ] Onboarding existing tenant user triggers platform admin and tenant admin notifications when enabled.
- [ ] Onboarding still returns success if the email provider throws.
- [ ] Onboarding emails never include password, token, access key, secret key, or hashed secret.
- [ ] `/metrics` includes email send counters after test sends.
- [ ] OpenAPI includes `Email` in the settings schema/example.

### Dashboard Tests

- [ ] Settings page renders the Email panel.
- [ ] Saving settings preserves `Email` fields.
- [ ] Mailgun API key field uses password input.
- [ ] Notification toggles update the outgoing settings payload.
- [ ] Raw JSON editor round-trips `Email`.

### SDK Tests

- [ ] C# `UpdateSettingsAsync` can submit a payload containing `Email`.
- [ ] JavaScript `updateSettings()` can submit a payload containing `Email`.
- [ ] Python `update_settings()` can submit a payload containing `Email`.

Run before marking implementation complete:

```powershell
dotnet build src/Xeno.sln
dotnet run --project src/Test.Automated -- --results results.json
dotnet test src/Test.Xunit
dotnet test src/Test.Nunit
```

If dashboard settings UI changes land:

```powershell
cd dashboard
npm test
```

Use the actual dashboard test command from `dashboard/package.json` if it differs.

## Implementation Phases

### Phase 1: Core Shape

- [ ] Add settings DTOs to `Xeno.Core.Settings`.
- [ ] Add `Email` to `XenoSettings`.
- [ ] Add `Xeno.Email` project and solution entry.
- [ ] Add neutral message models, provider interface, service interface, disabled provider, and file provider.
- [ ] Add template base and onboarding templates.
- [ ] Add unit tests for settings serialization, disabled provider, file provider, and templates.

Exit criteria: the solution builds, tests pass, and email can render/write to a file sink without provider credentials.

### Phase 2: Mailgun Provider

- [ ] Add `SendWithMailgun` dependency to `Xeno.Email`.
- [ ] Implement `MailgunEmailProvider`.
- [ ] Add provider factory validation.
- [ ] Add fakeable seam around provider send calls for tests.
- [ ] Document Mailgun settings.

Exit criteria: local test provider still works, Mailgun provider validates configuration, and no automated test needs live Mailgun credentials.

### Phase 3: Server Integration

- [ ] Reference `Xeno.Email` from `Xeno.Server`.
- [ ] Build email service from `_Settings.Email`.
- [ ] Update settings save/apply paths.
- [ ] Wire onboarding notifications after successful platform persistence.
- [ ] Add logs/audit and metrics.
- [ ] Add shared tests for onboarding side effects and failure behavior.

Exit criteria: onboarding still succeeds with email disabled, sends expected file-provider messages when enabled, and survives provider failure.

### Phase 4: Whole-Product Surfaces

- [ ] Update dashboard settings UI and tests.
- [ ] Update OpenAPI settings schema/example.
- [ ] Update `REST_API.md`, `README.md`, security docs, SDK READMEs, and Postman collection.
- [ ] Update Docker and sample settings files.
- [ ] Run full backend and dashboard verification.

Exit criteria: a developer can configure disabled/file/Mailgun email from settings, onboarding notifications are documented, and all product surfaces agree on the settings shape.

## Open Decisions

- [ ] Should `Email.Enabled = true` with `Provider = Disabled` be treated as a configuration error or as an explicit dry-run mode? Recommended: allow it and return skipped results.
- [ ] Should tenant-created welcome email go to the new tenant administrator by default? Recommended: yes, but without any secret material.
- [ ] Should platform admin recipients come only from settings, or should global admin users also receive notifications? Recommended first pass: settings only. Global admin user records may not contain real routable addresses in local/dev deployments.
- [ ] Should provider changes take effect immediately after settings save? Recommended first pass: restart required. Live rebuild can be added later.
- [ ] Should failed email sends create audit records? Recommended: yes for onboarding notifications, because those are administrative side effects worth seeing during support.

## Developer Completion Checklist

- [ ] `Xeno.Email` exists and builds.
- [ ] `Xeno.Email.Providers` contains disabled, file, and Mailgun providers.
- [ ] `Xeno.Email.Templates` contains base layout and onboarding templates.
- [ ] `Xeno.Server` depends only on `IXenoEmailService`, not Mailgun.
- [ ] Server JSON settings include `Email`.
- [ ] Dashboard settings can view/edit/preserve `Email`.
- [ ] Onboarding notifications are best-effort and non-secret.
- [ ] REST/OpenAPI/Postman/docs describe the settings and behavior.
- [ ] C#, JavaScript, and Python SDK settings paths still pass through email settings.
- [ ] Tests cover rendering, provider abstraction, settings persistence, onboarding wiring, dashboard preservation, and failure behavior.
- [ ] Full build and test commands from `AGENTS.md` pass.

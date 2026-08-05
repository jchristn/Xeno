# Cross-Tenant Shared Clients & Models

**Status:** MVP implemented on branch `feature/shared_clients` (see §16.3 for per-task status)
**Related:** [MODEL_ROUTING.md](MODEL_ROUTING.md), [SECURITY_BEST_PRACTICES.md](SECURITY_BEST_PRACTICES.md), [REST_API.md](REST_API.md), [WEBSOCKETS_API.md](WEBSOCKETS_API.md)

## 1. Overview & goal

Today every Xeno client (a WebSocket-connected node advertising models) is fenced to exactly one tenant. A caller in tenant B can only be routed to clients whose `TenantId` equals B's. This is enforced by hard `TenantId ==` equality filters throughout the serving path — there is no notion of a shared, global, or public client anywhere.

**Goal:** let a client that serves models in one tenant be made available as a routing target to other tenants, so a fleet of model-serving nodes can back a plethora of tenants — without collapsing the per-tenant isolation, RBAC scoping, or audit/ledger attribution that the rest of the deployment relies on.

**Non-goal:** giving a consuming tenant any *management* control over a client it does not own. Sharing grants compute, never control.

### Deployment assumption (important)

Shared nodes in this deployment may be operated by **either the SaaS operator or by customer-tenants themselves**. That second case is the hard one: when tenant B's request is routed to a node owned by tenant A, whoever operates A's node can observe B's prompts and completions in plaintext. The trust model in §2 exists to make that exposure **visible and controllable** — surfaced via provenance labels and governed by a consumer off switch — rather than silent.

---

## 2. Trust model

### 2.1 The data-plane reality

A shared client is a compute node that processes inference requests **in plaintext**. There is no technical mechanism that prevents the node's operator from logging prompts and completions it serves. So cross-tenant sharing carries a real data-exposure fact that no amount of authorization logic removes. The design's stance is deliberately lightweight rather than a heavyweight consent workflow: the owner makes a single decision to share; the exposure is made **visible** to consumers through provenance labels; and a consumer that cannot tolerate foreign processing has a blunt **off switch** (`ConsumeShared`). That is the whole trust surface — no operator approval gate, no per-node consumer opt-in.

This is a documented, deliberate exception to the core invariant in [SECURITY_BEST_PRACTICES.md](SECURITY_BEST_PRACTICES.md) that "every operation is bound to a tenant." That doc must be updated to describe the boundary precisely.

### 2.2 Single owner-side control

Sharing is **one owner-side action**. An owning tenant's admin marks a client `Shared` and it immediately becomes an eligible routing target for every tenant on the deployment — no operator approval step, no mandatory consumer opt-in. Unsharing removes eligibility for all subsequent routing decisions (not retroactive to in-flight requests).

Two lightweight properties round this out without gating the owner's decision:

- **Consumption is on by default, with an off switch.** A per-tenant `ConsumeShared` setting (default `true`) lets a consumer opt out of receiving *any* foreign-owned capacity. This exists for one concrete reason, not ceremony: tenants here are arms-length customers, and a shared node processes foreign prompts in plaintext, so a consumer that (for compliance or policy) must never send its data to another customer's hardware needs a way to say so. It defaults on, so "available to every tenant" is the out-of-the-box behavior; the switch never touches the happy path.
- **Provenance is shown, not enforced.** Each shared node's origin — operator-owned vs another tenant's — is surfaced to consumers and operators as a **label** (§2.3). It gates nothing; it just makes the `ConsumeShared` choice an informed one.

### 2.3 Provenance labels & the tenant-level sharing default

**Provenance (label only).** Every shared client carries an **origin** derived from its owner, shown to consumers and operators but gating nothing:

- **Operator** — owned by the operator's own tenant/infrastructure.
- **Third-party** — owned by another customer-tenant; consuming it exposes the consumer's prompts to a peer customer.

**Tenant-level sharing default.** Rather than force every client owner to flip a switch, each tenant has a `DefaultClientShared` setting that seeds the initial `Shared` value of its **newly connected** clients:

- When a client registers for the first time (new client id), its `Shared` is initialized to the owner tenant's `DefaultClientShared`.
- The client owner can then adjust that client's `Shared` value individually at any time (§6). The per-client value, once set, wins — it is never re-overwritten by the tenant default on reconnect (§3.5).
- Changing a tenant's `DefaultClientShared` affects only clients that connect *after* the change; it does not retroactively re-flag existing clients.

This lets an operator (or a tenant that wants all its nodes pooled) set "share everything I connect" once, while still allowing per-client exceptions.

### 2.4 What each party can and cannot see

- **Consumer** learns a shared node's **origin** ("operator" vs "third-party"), its operational id/name, availability, and advertised model names/capabilities — enough to make an informed choice and route. The consumer does **not** learn the owning tenant's identity, owner user/credential ids, weights, or raw load. (See the redaction projection, §5.)
- **Owner** sees aggregate borrowed load on its node (for capacity planning) but **not** per-request prompt content attributable to a consumer beyond what inherently passes through the node at inference time. The owner does not receive the consumer's tenant identity in the relayed request payload.
- **Operator (system admin)** sees everything, including the full mapping of shared nodes to consumers (built from the already-stamped `RequestorTenantId`).

### 2.5 Residual risks (documented, not eliminated)

- **Prompt/response harvesting** by a third-party node operator — mitigated only by provenance labeling, the consumer `ConsumeShared` off switch, and out-of-band ToS/legal. Not technically preventable: a shared node sees plaintext.
- **Silent exposure** — with consumption on by default, a consumer's traffic can route to a newly-shared third-party node without the consumer taking any action. Provenance labels and the off switch are the only mitigations; for sensitive, arms-length tenants an operator should weigh defaulting `ConsumeShared` off at the tenant level.
- **Poisoned completions** returned by a malicious owner node — mitigated by operator oversight and unshare/revocation.
- **Availability** — a borrowed node can disconnect at any time; consumers must treat shared capacity as best-effort. Eligibility already degrades gracefully on disconnect/heartbeat-staleness (`RoutingService.cs:222-269`).
- **Owner-node contention** — under the MVP FCFS model (§7), an owner's own requests compete with borrowers on the owner's own node. Owner-priority scheduling is deferred to phase 2 and called out as a known limitation.
- **Settlement/incentive** — third-party sharing donates compute; a credit/billing settlement between owner and consumer is out of scope for routing but is a prerequisite for tenant-owned sharing to make economic sense (§12).

---

## 3. Data model changes

All core entities live in a single JSON blob table `xeno_platform_state`, serialized with `System.Text.Json` (`JsonUtil`), enums as strings (`JsonStringEnumConverter`), missing members defaulting to their C# initializer. **Field additions are backward-compatible with no migration**: old blobs load with defaults applied and are re-saved on next boot (`DatabaseDriverBase.cs:107`). There is no state-blob version stamp and none is required for pure additions.

### 3.1 `XenoClientRecord` (`src/Xeno.Core/Models/XenoClientRecord.cs`)

```csharp
/// <summary>
/// Whether this client is an eligible routing target for tenants other than its owner.
/// Initialized at first registration from the owner tenant's DefaultClientShared (§3.4),
/// then adjustable per-client by the owner. Server-authoritative — never set by the node itself (§3.5).
/// Default: false (private to the owner tenant).
/// </summary>
public bool Shared { get; set; } = false;
```

Origin is **derived**, not stored: a node is *Operator*-origin iff its `TenantId` is the operator/default tenant (`Constants.DefaultTenantId`), else *Third-party*. (If operator infrastructure ever spans several tenants, replace the derivation with an explicit `OperatorOwned` bool.)

**A node is eligible-as-shared iff** `Shared && Active`. (Phase 2, for scoped sharing, would replace the `Shared` bool with a visibility enum plus a `SharedTenantIds` list; the bool is the MVP.)

### 3.2 `XenoClientModel` (`src/Xeno.Core/Models/XenoClientModel.cs`)

No change for MVP (whole-node sharing). Add `bool Shared` in phase 2 for per-model granularity.

### 3.3 `ModelDefinition` (`src/Xeno.Core/Models/ModelDefinition.cs`)

No change for MVP. Shared models resolve purely through the **client-advertised fallback path** (`RoutingService.cs:197-207`), which avoids the `(TenantId, Name)` dedupe question entirely (definitions are upserted from inventory at `XenoServer.cs:4987` and `5771`). Do **not** create cross-tenant `ModelDefinition` rows in MVP.

### 3.4 Tenant settings (`src/Xeno.Core/Models/Tenant.cs`)

```csharp
/// <summary>
/// Initial <see cref="XenoClientRecord.Shared"/> value applied to this tenant's clients when they
/// first connect (§2.3). Changing it affects only future connections, never existing clients.
/// Default: false.
/// </summary>
public bool DefaultClientShared { get; set; } = false;

/// <summary>
/// Whether this tenant consumes foreign-owned shared clients at all (§2.2 off switch). When false,
/// routing never considers any client the tenant does not own.
/// Default: true.
/// </summary>
public bool ConsumeShared { get; set; } = true;
```

### 3.5 Sharing-flag lifecycle & invariant (critical)

Sharing state is **server-authoritative** — the node never declares it in its register/inventory message. Precisely:

- **First registration** (new client id): the server sets `Shared` from the owner tenant's `DefaultClientShared` (§2.3).
- **Reconnect / inventory update** (existing client id): `RegisterClient` (`XenoServer.cs:5624`) and the inventory-update path (`XenoServer.cs:5693`) **must preserve** the existing per-client `Shared` value — they must not reset it to the tenant default and must not honor any sharing field in the message. Otherwise a tenant-owned node could self-promote (or a stale default could silently re-flag a node) and begin receiving foreign prompts.
- **Owner adjustment**: the only way to change a live client's `Shared` value is the owner endpoint in §6.

---

## 4. Serving / routing changes

Define one predicate and use it at every fence so the rule is identical everywhere:

```csharp
// Private helper — house style applies no XML doc to private methods.
private static bool VisibleTo(XenoClientRecord client, Tenant consumer)
{
    if (client.TenantId == consumer.Id) return true;   // own tenant — always visible
    if (!consumer.ConsumeShared) return false;         // consumer opted out of foreign capacity
    return client.Shared && client.Active;             // shared + live
}
```

Apply it at the four sites that today filter on `TenantId ==`:

| Site | File:line | Change |
|---|---|---|
| Candidate clients | `RoutingService.cs:87` | `Clients.Where(c => VisibleTo(c, consumer))` |
| Model resolution (client fallback) | `RoutingService.cs:197, 206` | same predicate |
| Dispatch re-check before proxy | `XenoServer.cs:2895` | `c.Id == decision.SelectedClientId && VisibleTo(c, consumer)` |
| Relay path re-check | `XenoServer.cs:4252` | same predicate |

Everything else in routing is unchanged:

- **Eligibility** (`IsEligibleClient`/`BuildClientExclusionReason`, `RoutingService.cs:222-269`) still gates on connected / heartbeat freshness / capacity / model-advertised / provider-match.
- **Routing policy** resolution stays consumer-scoped (`RoutingService.cs:492`). A shared client is just another candidate in the consumer's pool; the consumer's own policy decides load-balancing. No cross-tenant policy is introduced.

---

## 5. Privacy & redaction projection (new subsystem)

Today `GET /api/v1/clients` (`XenoServer.cs:2325`) and the dashboard detail modal serialize the **raw `XenoClientRecord`** — there is no DTO/redaction layer for clients (unlike `RedactUser`/`RedactCredential`). Widening visibility without a projection would leak, to a consumer, the owner tenant's `TenantId`, `OwnerUserId`, `OwnerCredentialId`, `OwnerPrincipalType`, `Weight`, `InFlightRequests`, `MaxParallelRequests`, and full inventory metadata.

Introduce **`ClientView`** (implemented as `src/Xeno.Core/Models/ClientView.cs`), applied server-side whenever a client is rendered to a **non-owner** tenant:

| Field | Own-tenant view | Non-owner (shared) view |
|---|---|---|
| `Id`, `Name` | shown | shown |
| `Origin` (new) | `"tenant"` | `"operator"` \| `"third-party"` |
| `TenantId`, `Owner*` | shown | **omitted** |
| `Weight`, `InFlightRequests`, `MaxParallelRequests` | shown | **omitted** |
| `Connected`, `ServiceState`, `LastHeartbeatUtc` | shown | shown |
| `Providers`, `Models` | full | model **names + capability flags only** (drop `Source`, management flags, `LastAdvertisedUtc`) |

Redaction happens **in the endpoint**, never in the UI — the dashboard detail modal is a generic key/value dump (`App.jsx:5728`) that renders whatever it receives, so the server is the only safe place to enforce it. This is the single highest-priority security item in the feature.

**While here:** `GET /api/v1/modeldefinitions` and `GET /api/v1/modelconfigurations` currently have **no permission gate** (`XenoServer.cs:2544-2554`) — only a tenant filter. Add a `RequirePermissionAsync` gate before widening their scope.

---

## 6. API surface changes

### New write-path (there is no client write-API today — nodes self-register over WebSocket)

- `POST /api/v1/clients/{id}/sharing` — **owner-tenant admin**. Body `{ shared: true | false }`. Sets the per-client `Shared` value; writes an audit record. Rejects callers who are not the owner tenant.
- `PUT /api/v1/tenants/{id}` (existing tenant update) gains `defaultClientShared` and `consumeShared`, both settable by the tenant admin. Changing `defaultClientShared` affects only future connections (§2.3).
- `GET /api/v1/admin/shared-clients` — **system admin**. Cross-tenant list of all shared nodes, origins, and consumers (from `RequestorTenantId`).

### Widened read-paths (apply `VisibleTo` + `ClientView` for non-owner rows)

- `GET /api/v1/clients` (`XenoServer.cs:2325`) — own + eligible shared; shared rows projected.
- `GET /v1/models` (`XenoServer.cs:2600`) & `GET /api/tags` (`XenoServer.cs:3208`) — aggregate over the widened set. Set `owned_by:"shared"` on `/v1/models` shared-origin entries as a provenance hint (no tenant leak). Group-by-name merges shared models into the consumer's catalog naturally.
- `GET /v1.0/api/client-telemetry/status` (`XenoServer.cs:3562`) — report shared eligibility **separately** (`sharedEligibleClientCount`) rather than folding it into the own-tenant counts.

### Management stays owner-fenced

`DELETE /clients/{id}` (`2332`), `client-models/pull` (`2349`), `client-models` delete (`2341`) must reject callers who are not the owner tenant, even though the node is now *visible* to them. Visibility ≠ control.

---

## 7. Capacity & fairness

**MVP decision: first-come-first-served on the shared pool, documented.** Consolidating to a single shared record fixes the N×-oversubscription that per-tenant re-registration would cause (one record = one real `MaxParallelRequests` budget). It introduces cross-tenant contention on that budget, accepted for v1.

Known limitations to document in [MODEL_ROUTING.md](MODEL_ROUTING.md):

- A noisy consumer can transiently monopolize a shared node's `MaxParallelRequests`.
- Under FCFS an **owner's own requests compete with borrowers on the owner's own node** — there is no owner-priority in v1.

Phase 2: per-tenant in-flight caps on shared clients (cheap to add in `SelectClient`/eligibility using the already-stamped `RequestorTenantId`), owner-priority reservation, and weighted-fair queueing.

---

## 8. Naming & collisions

Requests select a model by **string** (`"model": "llama3.2:latest"`). Merging shared models by bare name is the desired behavior (more capacity), with one sharp edge: if a consumer runs a *different* model under the same name, Xeno cannot distinguish them and will pool both.

- **MVP:** pool by bare name; document the risk; the `ConsumeShared = false` setting is the escape hatch for tenants that cannot tolerate any pooling.
- **Phase 2:** expose shared models under a qualified alias (e.g. `shared:llama3.2`) in catalogs and resolve the qualified form to shared-only candidates. OpenAI/Ollama clients pass arbitrary model strings, so this stays wire-compatible.

---

## 9. Dashboard UX (React SPA — `dashboard/src/App.jsx`)

### Consumer tenant

- **`ClientsView`** (`App.jsx:2527`) — today shows no tenant/owner column. Add an **origin badge** column (`Operator` / `Third-party` / own); render shared rows muted; **suppress management actions** (Delete, Pull Model, telemetry-config) on shared rows.
- **`AdvertisedModelsView`** (`App.jsx:2594`) — add `origin` into `flattenClientModels` (`App.jsx:7663`) and badge shared model rows.
- **Detail modal** (`App.jsx:5728`) — protected by the server-side projection (§5); optionally add a bespoke shared-client card instead of the raw key/value dump.
- **`RoutingPoliciesView`** editor (`App.jsx:6309`) — shared clients selectable by `clientName`, grouped/badged as shared in the picker.
- **Settings** — surface the tenant `ConsumeShared` toggle (default on) with a note that turning it off isolates the tenant from all foreign capacity, plus the `DefaultClientShared` toggle that seeds newly-connected clients.

### Owner tenant

- A per-client `Shared` toggle in `ClientsView` (or the detail view), backed by `POST …/sharing` — flips a node in/out of the shared pool.
- A tenant-level `DefaultClientShared` toggle in Settings that seeds newly-connected clients (§2.3).
- Aggregate borrowed-load view for capacity planning (broken down by consumer only for the operator, not for a peer owner).

### System admin

- A **Sharing Admin** view: all shared nodes across tenants with their origin, and consumption by `RequestorTenantId`. Reuse the existing **Global scope** precedent already built into `ModelLeaderboardView` (`App.jsx:3278-3297`) — the platform's established pattern for an admin-only cross-tenant view.

New i18n keys in `dashboard/src/i18n/resources.js` (`shared`, `origin`, `operatorOrigin`, `thirdPartyOrigin`, `consumeShared`, `defaultClientShared`, …). Gate admin controls on the existing `isSystemAdmin` notion.

---

## 10. SDK changes (`src/sdk/{csharp,python,javascript}`)

- **Python & JS** pass responses through untyped, so new fields appear automatically — **zero code change** for read.
- **C# typed DTOs** (hand-maintained thin copies, must be edited):
  - `Models/XenoClientRecord.cs` — add `Shared` and `Origin`. Its DTO already omits `Weight`/`InFlight`/`MaxParallel`, aligning with the redacted projection.
  - `Models/OpenAiModelList.cs` — entries are `RawJsonValue`, so an `owned_by:"shared"` marker is invisible to typed consumers; add a typed model-entry class if C# users should filter by provenance.
- **New methods** (all three SDKs): `SetClientSharingAsync(id, shared)` (owner); the tenant-update call gains `defaultClientShared`/`consumeShared`; optionally `ListSharedClientsAsync()`.
- **Serving node** (`src/Xeno.Client`, the `xeno` CLI) — **no change for MVP**: sharing is set via the management API/dashboard, not declared by the node in its register/inventory message (see the sharing-flag lifecycle, §3.5).
- **Consuming a shared model** needs no SDK change — the request body model name is already an untyped string.

---

## 11. Telemetry, ledger & billing

Request records already stamp **both** `TenantId` (serving/owner) and `RequestorTenantId` (consumer) — `RoutingService.cs:164-165`, `XenoServer.cs:2956-2957`. Therefore:

- **Attribution** can charge the consumer while noting the serving node's owner with no schema change.
- The **leaderboard** already has a Global scope; shared-usage rollups fit its model.
- Decide explicitly: the owner sees **aggregate** borrowed load (yes) but **not** per-request prompt metadata attributable to a consumer (no). The `RequestorTenantId` split makes both cheap.

---

## 12. Settlement (out of scope, prerequisite for third-party sharing)

Third-party sharing donates one tenant's compute to another. A credit/settlement mechanism (owner earns, consumer pays) is **out of scope for routing** but is a business prerequisite before enabling third-party (tenant-owned) sharing broadly in production. Flagged here so it is not forgotten; the `RequestorTenantId`/`TenantId` stamps already provide the metering substrate.

---

## 13. Security review checklist

- [ ] Sharing state is server-authoritative; register/inventory messages cannot set it; first-connect seeds from tenant default, reconnect preserves the per-client value (§3.5).
- [ ] `ClientView` redaction applied on every non-owner read path; no owner identity, credentials, weights, or raw load leak (§5).
- [ ] Owner identity/tenant is never revealed to a consumer — only origin (§2.4).
- [ ] `ConsumeShared` defaults on but is honored when off; management endpoints reject non-owner callers even for visible shared nodes (§6).
- [ ] Toggling a client's `Shared` value writes an audit record.
- [ ] The two ungated model-definition endpoints are gated (§5).
- [ ] Relayed inference payloads to a shared node carry no consumer tenant identity (§2.4).

---

## 14. Backward compatibility & rollout

All fields are additive and default to isolated behavior: client `Shared = false`, tenant `DefaultClientShared = false`. (Consumers default `ConsumeShared = true`, but with nothing shared yet there is nothing to consume.) After upgrade, **every existing deployment stays fully isolated** until an owner marks a client — or a tenant sets its default — to shared. No migration, no version bump.

Safe rollout order:

1. Ship data model + sharing-flag lifecycle/invariant (§3).
2. Ship the `ClientView` server-side projection **before** any read-path widening (§5).
3. Widen routing with `VisibleTo`, verify with one pilot: mark one operator-owned node `Shared`, consume from a second tenant.
4. Add the per-client toggle, tenant `DefaultClientShared`/`ConsumeShared` settings, and dashboard surfaces.
5. Enable third-party (tenant-owned) sharing broadly only after settlement (§12) and fairness caps (§7 phase 2) are in place.
6. SDK + docs.

---

## 15. Docs to update

- [MODEL_ROUTING.md](MODEL_ROUTING.md) — shared eligibility, FCFS/owner-contention limitations.
- [SECURITY_BEST_PRACTICES.md](SECURITY_BEST_PRACTICES.md) — the new cross-tenant trust boundary and its exception to "every operation bound to a tenant."
- [REST_API.md](REST_API.md) — sharing endpoints, widened lists, `ClientView` schema.
- [WEBSOCKETS_API.md](WEBSOCKETS_API.md) — note that sharing is **not** client-declared.
- All three SDK READMEs, `README.md` / `DOCKERHUB_README.md` positioning, `CHANGELOG.md`.

---

## 16. Implementation plan

Code in this section follows `c:\code\agents\requirements\CODE_STYLE.md`: explicit types (no `var`), no tuples, XML documentation on every public member, private members Pascal-cased with a leading underscore, one class or enum per file, and **no `System.Text.Json` DOM types** (`JsonElement`/`JsonNode`/`JsonDocument`) — the existing `JsonUtil` serializer remains the only JSON entry point, so the new fields serialize through the platform-state blob with no DOM manipulation.

### 16.1 Phasing

**MVP** — client `Shared` field + tenant `DefaultClientShared`/`ConsumeShared` + the sharing-flag lifecycle/invariant · `VisibleTo` at the four routing sites · `ClientView` server-side redaction · widened `/clients`, `/v1/models`, `/api/tags` · per-client sharing endpoint · dashboard badges, owner per-client toggle, tenant settings, admin cross-tenant view · gate the two ungated model-definition endpoints · FCFS capacity (documented). The diff concentrates in `RoutingService.cs` (4 sites) and `XenoServer.cs` (dispatch re-check, endpoint handlers, projection, new endpoints), plus two tenant fields and one client field.

**Phase 2** — per-tenant fairness caps + owner-priority · per-model sharing · qualified model aliases · owner consumption telemetry · scoped sharing (visibility enum + `SharedTenantIds`) · settlement/metering.

### 16.2 Task checklist

Each box carries a status marker: `[ ]` not started, `[~]` in progress, `[x]` done. Annotate a finished task in place — for example `- [x] **DM-1** … _(initials, PR #421, 2026-08-07)_`. Tasks are ordered so a workstream can start once the boxes it depends on are checked; the dependency notes call out the few cross-workstream ordering constraints.

**Data model — `src/Xeno.Core/Models`**
- [ ] **DM-1** Add `bool Shared` to `XenoClientRecord` (default false), XML-documented per §3.1.
- [ ] **DM-2** Add `bool DefaultClientShared` (default false) and `bool ConsumeShared` (default true) to `Tenant`, per §3.4.
- [ ] **DM-3** Verify blob round-trip: a pre-change `xeno_platform_state` row loads with the new fields defaulted and re-saves on boot (no migration, no version bump).

**Registration lifecycle — `src/Xeno.Server/XenoServer.cs`** _(depends on DM-1, DM-2)_
- [ ] **REG-1** First-registration seed: when `RegisterClient` (`:5624`) creates a new record, set `Shared` from the owner tenant's `DefaultClientShared`.
- [ ] **REG-2** Reconnect/inventory preserve: `RegisterClient` (`:5624`) and the inventory-update path (`:5693`) must not overwrite `Shared` and must ignore any sharing field arriving in the message (§3.5).

**Routing — `src/Xeno.Core/Services/RoutingService.cs`, `XenoServer.cs`** _(depends on DM-1, DM-2)_
- [ ] **RT-1** Add the `private static bool VisibleTo(client, consumer)` helper (§4).
- [ ] **RT-2** Candidate filter — `RoutingService.cs:87` → `VisibleTo`.
- [ ] **RT-3** Model-resolution client fallback — `RoutingService.cs:197,206` → `VisibleTo`.
- [ ] **RT-4** Dispatch re-check `XenoServer.cs:2895` and relay re-check `XenoServer.cs:4252` → `VisibleTo`.
- [ ] **RT-5** Load the consuming `Tenant` once in `RouteAsync` and thread it through — no per-candidate tenant lookups.

**Redaction — `XenoServer.cs`, `src/Xeno.Core/Models`** _(RD-1/RD-2 must ship before any widened read path — §14 step 2)_
- [ ] **RD-1** Add the `ClientView` projection type, fields per §5, one class per file, public members XML-documented.
- [ ] **RD-2** `GET /api/v1/clients` (`:2325`): own records raw, foreign shared records projected. Assert no owner identity, credentials, weights, or raw load in the projected shape.
- [ ] **RD-3** Populate `Origin` (`tenant`/`operator`/`third-party`), derived from `TenantId == Constants.DefaultTenantId`.

**API surface — `XenoServer.cs` + route registration**
- [ ] **API-1** `POST /api/v1/clients/{id}/sharing` (owner-only): body `{ shared }`, reject non-owner callers, write an audit record.
- [ ] **API-2** Tenant update accepts `defaultClientShared` / `consumeShared`.
- [ ] **API-3** `GET /api/v1/admin/shared-clients` (system-admin): cross-tenant shared list + consumers from `RequestorTenantId`.
- [ ] **API-4** Widen `/v1/models` (`:2600`) and `/api/tags` (`:3208`) over the `VisibleTo` set; mark shared-origin entries (`owned_by:"shared"`). _(depends on RT-1)_
- [ ] **API-5** Add a permission gate to `/api/v1/modeldefinitions` (`:2544`) and `/api/v1/modelconfigurations` (`:2550`) before their scope widens (§5).
- [ ] **API-6** `client-telemetry/status` (`:3562`) reports `sharedEligibleClientCount` separately from own-tenant counts.
- [ ] **API-7** Management endpoints — delete client (`:2332`), model pull (`:2349`), model delete (`:2341`) — reject non-owner callers even for visible shared nodes.

**Dashboard — `dashboard/src`** _(depends on RD-2, API-1/2/3)_
- [ ] **DASH-1** `ClientsView` (`App.jsx:2527`): origin badge column; hide management actions on foreign shared rows.
- [ ] **DASH-2** `AdvertisedModelsView` (`App.jsx:2594`): add `origin` in `flattenClientModels` (`:7663`); badge shared rows.
- [ ] **DASH-3** Owner per-client `Shared` toggle → `POST …/sharing`.
- [ ] **DASH-4** Settings: tenant `DefaultClientShared` and `ConsumeShared` toggles with the isolation note (§9).
- [ ] **DASH-5** Admin Sharing view, reusing the Global-scope pattern at `App.jsx:3278-3297`.
- [ ] **DASH-6** i18n keys in `resources.js` (`shared`, `origin`, `operatorOrigin`, `thirdPartyOrigin`, `consumeShared`, `defaultClientShared`).

**SDK — `src/sdk/{csharp,python,javascript}`** _(depends on API-1, RD-3)_
- [ ] **SDK-1** C# DTOs: add `Shared`/`Origin` to `Models/XenoClientRecord.cs`, XML-documented; Python/JS pass through untyped.
- [ ] **SDK-2** `SetClientSharingAsync(id, shared)` in all three SDKs.
- [ ] **SDK-3** Tenant-update methods carry `defaultClientShared` / `consumeShared`.
- [ ] **SDK-4** Update the three SDK READMEs.

**Tests — `src/*.Test`**
- [ ] **TEST-1** `ConsumeShared=true` consumer routes to an operator shared node; `false` never does.
- [ ] **TEST-2** Foreign shared client in `GET /clients` is projected — owner ids / weights / load asserted absent.
- [ ] **TEST-3** Reconnect does not flip a manually toggled `Shared` value (guards REG-2).
- [ ] **TEST-4** Non-owner `POST …/sharing`, delete, and model-pull are all rejected.
- [ ] **TEST-5** Pre-change state blob loads and re-saves with the new fields defaulted (guards DM-3).

**Gates before third-party (tenant-owned) sharing is enabled broadly**
- [ ] Every box in the §13 security review checklist is checked.
- [x] Every doc in the §15 list is updated.
- [ ] Settlement/metering (§12) and phase-2 fairness caps (§7) are in place.

### 16.3 Implementation status — branch `feature/shared_clients`

MVP is implemented and the C# solution and dashboard both build clean. The `Routing` test suite passes 19/19, including four new cases (`SharedClientRoutesToConsumingTenant`, `NonSharedClientDoesNotRouteCrossTenant`, `ConsumeSharedDisabledIsolatesTenant`, `LegacyStateBlobDefaultsSharingFields`).

**Done**
- **DM-1/2/3** — `XenoClientRecord.Shared`; `Tenant.DefaultClientShared`/`ConsumeShared`; legacy-blob defaults verified by `LegacyStateBlobDefaultsSharingFields`.
- **REG-1/2** — first-connect seeds `Shared` from `DefaultClientShared` (`RegisterClient`); reconnect/inventory preserve the per-client value (the node never declares sharing).
- **RT-1..5** — `VisibleToTenant` in `RoutingService`; applied at the candidate filter, both model-resolution fallbacks, and both `XenoServer` dispatch re-checks; consumer tenant resolved once per route.
- **RD-1/2/3** — `ClientView` projection; `GET /clients` returns own records in full and foreign shared clients redacted (no owner identity/weights/load); `Origin` populated.
- **API-1..7** — `POST /clients/{id}/sharing` (owner-fenced, audited); tenant `defaultClientShared`/`consumeShared` on create/update; `GET /admin/shared-clients` (system-admin); `/v1/models` + `/api/tags` widened with `owned_by:"shared"`; model-definition/-configuration endpoints gated (`Inference` Read); `sharedEligibleClientCount` on telemetry status; management endpoints already owner-fenced via `RequireTenantClient`/`DeleteClient`.
- **DASH-1..6** — origin badge + suppressed foreign-row actions in `ClientsView`; advertised-model badge; owner `Shared` toggle; tenant `DefaultClientShared`/`ConsumeShared` settings with isolation note; i18n across all 6 locales.
- **SDK-1..4** — C# `XenoClientRecord.Shared`/`Origin`, `Tenant` fields, `SetClientSharingAsync`; Python `set_client_sharing`; JS `setClientSharing`; READMEs.
- **TEST-1, TEST-5** — cross-tenant visibility gating and legacy-blob defaults.
- **Docs** — MODEL_ROUTING, SECURITY_BEST_PRACTICES, REST_API, WEBSOCKETS_API, three SDK READMEs, CHANGELOG, README, DOCKERHUB.

**Deferred (need the full server + WebSocket integration harness; tracked, not yet written)**
- [ ] **TEST-2** — assert `GET /clients` redacts a foreign shared client (owner ids/weights/load absent) end-to-end over HTTP.
- [ ] **TEST-3** — assert reconnect does not flip a manually toggled `Shared` value (REG-2) through the live registration path.
- [ ] **TEST-4** — assert non-owner `POST …/sharing`, delete, and model-pull are rejected over HTTP.

Note: the redaction (RD) and owner-fence (API-1/7) logic is implemented and exercised indirectly, but the three end-to-end assertions above should be added in the `ManagementApi` suite before third-party sharing is enabled in production.

---

## 17. Open questions

1. Is "operator origin" adequately identified by `TenantId == DefaultTenantId`, or does operator infrastructure span multiple tenants (needing an explicit `OperatorOwned` flag)?
2. Does a consumer need to know *which* third-party owns a shared node (marketplace transparency) versus only its origin (operator vs third-party)? Current design reveals only the origin.
3. Settlement model for third-party sharing — credits, revenue share, or flat operator-brokered pricing? (§12)
4. Should an owner be able to cap how much of its node's capacity borrowers may consume, even in MVP? (Currently phase 2.)

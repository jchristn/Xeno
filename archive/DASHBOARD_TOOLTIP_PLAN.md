# Dashboard Tooltip Coverage Plan

This plan covers the Xeno dashboard tooltip pass. The objective is that every
interactive element on every dashboard page has hover help that explains what
the element does and how it is used. Tooltip text must not merely restate the
visible label.

## Coverage Standard

Every dashboard surface must provide descriptive hover help for:

- navigation links
- external links
- icon buttons
- text buttons
- table row action menus
- table action menu items
- search boxes
- column filters
- sortable headers
- pagination controls
- auto-refresh dropdowns
- page-size dropdowns
- chart range buttons
- chart bars and hover targets
- copy-to-clipboard controls
- language selectors
- modal close, save, cancel, confirm, and delete buttons
- modal header copy controls
- form textboxes
- form numeric inputs
- form password inputs
- form checkboxes and toggles
- form dropdowns
- JSON textareas
- backup and restore textareas
- API Explorer endpoint selectors
- API Explorer operation selectors
- API Explorer path parameter fields
- API Explorer query JSON editors
- API Explorer request body JSON editors
- API Explorer response tabs
- API Explorer execute, stop, and clear controls
- Settings page controls for REST, CORS, WebSocket, database, logging, backup,
  restore, and raw JSON config editing

## Implementation Strategy

1. Add localizable tooltip strings to `dashboard/src/i18n/resources.js`.
2. Add explicit `title` text to shared controls where the component understands
   the control intent:
   - `ActionMenu`
   - `Pagination`
   - `CopyButton`
   - `CopyableValue`
   - `LanguageSelector`
   - `Modal`
   - `ConfirmModal`
   - `ActivityChart`
   - `ConnectedClientsChart`
3. Add explicit page-level tooltips where controls have product-specific
   meaning:
   - top navigation
   - topbar GitHub/theme/logout/server URL controls
   - login actions
   - API Explorer controls
   - Settings page controls
   - create/edit modals
4. Add a dashboard-wide tooltip backfill hook for dynamically generated or
   missed interactive elements. The hook assigns descriptive localized tooltip
   text based on control type and discovered label text. This ensures coverage
   for every rendered `button`, `a`, `input`, `select`, `textarea`, and
   `[role="button"]`, including controls created by reusable tables and modals.
5. Run a dashboard build to verify the tooltip pass does not break rendering.

## Page Checklist

Home:

- Request Activity range buttons and chart bars explain range selection and
  bucket values.
- Connected Clients range buttons and chart bars explain connected-node samples.

Request History:

- Search, column filters, sort headers, row actions, JSON view, delete, refresh,
  pagination, and auto-refresh controls explain their effect.
- Request Details modal close and copy controls explain captured diagnostics.

API Explorer:

- Endpoint dropdown explains OpenAPI tag filtering.
- Operation dropdown explains operation selection and example body updates.
- Path parameter fields explain path substitution.
- Query JSON and Request Body JSON editors explain request construction.
- Execute, Stop, Clear, and response tabs explain behavior, including streamed
  responses.

Clients:

- Client table controls explain search, filtering, sorting, refresh, row edit,
  JSON view, delete, pagination, and auto-refresh.

Advertised Models:

- Client filter dropdown explains model inventory scoping.
- Model table controls explain search, filtering, sorting, refresh, row edit,
  JSON view, delete, pagination, and auto-refresh.

Tenants:

- Create Tenant, tenant table controls, row actions, modal fields, active toggle,
  save, close, and JSON copy controls explain their impact.

Users:

- Create User, user table controls, row actions, modal fields, admin toggles,
  password reset field, save, close, and JSON copy controls explain their
  impact.

Credentials:

- Create Credential, credential table controls, row actions, modal fields, secret
  rotation field, active toggle, save, close, and JSON copy controls explain
  their impact.

Security:

- Role and Permission create buttons, tables, row actions, modal fields,
  permission picker checkboxes, operation dropdown, permit toggle, active toggle,
  effective-permission table, and audit table controls explain their impact.

Analytics:

- Request activity range buttons, grouped request tables, row actions, refresh,
  pagination, auto-refresh, and metrics copy controls explain their purpose.

Settings:

- REST controls explain restart-bound listener settings versus live
  authentication behavior.
- CORS controls explain browser access and preflight behavior.
- WebSocket controls explain listener path restart requirements and live timeout
  behavior.
- Database controls explain provider, connection, and restart impact.
- Logging controls explain output destinations and restart impact.
- Backup/restore controls explain export and import behavior.
- Raw config JSON controls explain direct config editing and restart-bound keys.

## Verification

- Confirm `npm run build` succeeds.
- Inspect a representative rendered page after build if manual browser testing
  is available.
- Use the DOM backfill hook as a safety net for any dynamic control introduced
  later.

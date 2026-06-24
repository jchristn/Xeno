# Xeno Dashboard Reference Audit

Date: 2026-06-24.

This audit covers two additional review passes against:

- `C:\code\conductor\conductor`
- `C:\code\assistanthub`
- `C:\code\verbex`
- `C:\code\lattice`
- `C:\code\pneuma`

## Pass 1: Component Inventory

The reference dashboards consistently use these dashboard primitives:

| Area | Reference evidence | Xeno status after pass |
| --- | --- | --- |
| App shell | Conductor, AssistantHub, Lattice, Verbex, and Pneuma all use a fixed sidebar plus topbar/header pattern. | Present: grouped sidebar, topbar context, logo in upper-left sidebar. |
| Topbar actions | Conductor/AssistantHub/Verbex include server badges, theme, GitHub, logout/disconnect, copy server URL. | Present: server badge, copy server URL, GitHub, theme toggle, logout. |
| Copyable IDs | Conductor, AssistantHub, Verbex, Lattice, Pneuma use fixed-width copyable ID/value components. | Present: `CopyButton`, `CopyableValue`, table/detail ID detection, HTTP fallback clipboard. |
| Modal system | All references use reusable modal shells; several include confirm, alert, JSON/detail variants. | Present: `Modal`, `ConfirmModal`, JSON/detail modal composition. Alert/toast still future work. |
| Tables | Conductor, AssistantHub, Verbex, Pneuma use sorting, filtering, pagination, row actions, empty/loading states. | Present: shared DataTable now has global search, per-column filters, sorting, pagination, row actions, empty state. |
| Row actions | Conductor, AssistantHub, Verbex, Pneuma use row action menus to keep tables dense. | Present: `ActionMenu` with row "View details" action. |
| Pagination | AssistantHub, Verbex, Lattice, Pneuma have reusable pagination components and page-size controls. | Present: `Pagination` component. |
| Empty/loading states | AssistantHub/Verbex/Pneuma use explicit empty panels instead of bare table rows. | Present: `EmptyState`. Loading states remain limited to current resource fallback behavior. |
| Request charts | Conductor and Pneuma include request chart panels with ranges and hover detail. | Present: range controls, labeled axes, ticks, legend, hover/focus detail. |
| Security/admin pages | Conductor, AssistantHub, Verbex, Pneuma split tenants/users/credentials/security into operational views. | Present: standalone Tenants, Users, Credentials, Security views. |
| Health visualization | Conductor/AssistantHub/Pneuma include health histograms/detail panels. | Missing: Xeno has status chips but not a health histogram component. |
| Setup/tour | Conductor, AssistantHub, Verbex, Lattice, Pneuma include tours/setup wizards. | Missing: Xeno does not yet include guided setup/tour. |
| Form modals | References use create/edit form modals per entity. | Partial: Xeno has actions and details, but create/edit forms need backend route coverage and validation. |
| Toasts/alerts | Pneuma has toast provider; AssistantHub/Verbex have alert modals. | Missing: Xeno has confirm modal but no toast/alert provider yet. |

## Pass 2: Xeno Improvements Implemented

The second pass added or hardened the following Xeno dashboard primitives:

- `dashboard/src/components/ActionMenu.jsx`
- `dashboard/src/components/ConfirmModal.jsx`
- `dashboard/src/components/CopyButton.jsx`
- `dashboard/src/components/CopyableValue.jsx`
- `dashboard/src/components/EmptyState.jsx`
- `dashboard/src/components/Modal.jsx`
- `dashboard/src/components/Pagination.jsx`
- `dashboard/src/utils/clipboard.js`

The central `DataTable` now provides:

- global text search;
- per-column filters;
- sortable column headers;
- page-size controls;
- first/previous/next/last pagination;
- row action menu;
- shared empty state;
- row-click protection for buttons, links, inputs, copy controls, and menus;
- automatic fixed-width copy controls for ID/key-like columns.

The detail/JSON surfaces now provide:

- reusable portal modal shell;
- Escape and backdrop close;
- body scroll locking;
- header copy action;
- structured JSON rendering;
- copyable ID/key-like values inside nested JSON.

## Remaining Dashboard Parity Work

These items are still not done and should be treated as real dashboard debt:

- Add entity create/edit form modals for tenants, users, credentials, clients, endpoints, models, VMRs, policies, and reservations.
- Add delete/archive flows where the backend supports them.
- Add alert modal or toast provider for operation success/failure feedback.
- Add loading/error state propagation from `useResource` instead of silent fallbacks.
- Add health histogram/detail panels for connected clients and model runner endpoints.
- Add setup wizard and guided tour if Xeno is expected to match the onboarding depth of the references.
- Add visual regression screenshots for desktop, tablet, and mobile.
- Add interaction tests for table sorting/filtering/pagination/copy/modals.

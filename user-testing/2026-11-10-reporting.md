# Simulated user testing: usage analytics, 2026-11-10

A focused session on the usage analytics work, not a full release session. It covers the REST routes, `xeno usage`, and connected client sampling on an isolated instance.

## Environment

| Item | Value |
|---|---|
| Commit | `a124d07` plus uncommitted usage analytics changes |
| Platform | macOS (Darwin 25.6.0), .NET 10, Debug build from the checkout |
| Server | Isolated, loopback port 9700, SQLite, temporary folder, copy of `xeno.json` |
| Client | Isolated `XENO_CLIENT_HOME`, dashboard port 19343, no providers |
| Real install | Not touched during the session. `~/.xeno` was not used |

Users simulated: operator (REST and CLI) and an unauthenticated caller.

## Summary

Four findings: no S1 or S2, one S3, three S4. Setup took a few minutes. The instance had no inference traffic, so every chart returned empty data; charts with data are covered by the automated suites, not by this session.

## Coverage

| Surface | Item | Status |
|---|---|---|
| REST (server) | `request-history/analytics/usage`, `chat/analytics/usage`, `client-telemetry/providers/health`, `client-telemetry/connected-history` | Covered: 200 with a token, shape checked |
| REST (server) | No credentials | Covered on `connected-history`: 401 |
| REST (server) | Bad date, zero bucket, reversed range, too many buckets | Covered: 400 with a clear message |
| REST (server) | Tenant the caller does not belong to | Covered: 401 |
| REST (server) | `/openapi.json` lists the four routes | Covered |
| REST (client) | Four local usage and health routes | Covered: 200 with the token, 401 without |
| CLI | `xeno usage` client stopped, `--help`, bad `--range`, all three datasets, `--json` | Covered |
| Server | Connected client sampling | Covered: count went from 0 of 1 to 1 of 2 after the client connected; empty minutes stay empty |
| Web dashboards | Usage, telemetry, and Home charts | Not covered here: Playwright with fixtures only |
| Desktop app | Usage page | Not covered: no headless rendering in this environment |
| Terminal app | Usage page | Not covered here: `TuiHarness` suites only |
| MCP, SDKs | `xeno_usage`, SDK methods | Not covered here: automated suites only |
| Docs | Postman collection run as written | Not covered: request URLs checked against the routes above, collection not executed |
| Install | Clean install through each documented path | Not covered |

## Findings

### UT-001: `xeno status` prints the access key in full

- **Severity:** S3
- **Category:** Security hygiene
- **Surface:** CLI
- **User:** Operator
- **Oracle:** Common sense; secrets should not appear in routine status output
- **Frequency:** Always

**Steps:** Sign in with an access key and run `xeno status`.
**Expected:** The key is masked or left out.
**Actual:** The `Auth:` line shows the whole key, so it ends up in terminal scrollback and pasted output.
**Evidence:** Observed in the session transcript; value not reproduced here.
**Suggested direction:** Show only a short prefix.

### UT-002: `xeno status` says Background: true after the client stops

- **Severity:** S4
- **Category:** Defect
- **Surface:** CLI
- **User:** Operator
- **Oracle:** `xeno disconnect` reports the client is not running
- **Frequency:** Seen once, after `xeno disconnect` and with the server stopped

**Steps:** Run `xeno connect`, then `xeno disconnect`, then `xeno status`.
**Expected:** `Background: false`, or no such line.
**Actual:** `Connected: false` and `Background: true`, because `state.json` keeps `IsBackground`.
**Suggested direction:** Clear the flag on shutdown, or check the process.

### UT-003: CLI errors go to stdout

- **Severity:** S4
- **Category:** Inconsistency
- **Surface:** CLI
- **User:** Operator
- **Oracle:** Shell convention; a script reading `xeno usage --json` gets error text instead of JSON
- **Frequency:** Always

**Steps:** Run `xeno usage --json` with the client stopped and capture stdout.
**Expected:** The error on stderr, with a non-zero exit.
**Actual:** Exit code 1 is correct, but the shared `WriteError` writes to stdout.
**Suggested direction:** Send `WriteError` to stderr; it affects every command.

### UT-004: Server settings fall back silently when the settings file is missing

- **Severity:** S4
- **Category:** Usability
- **Surface:** Server
- **User:** Operator
- **Oracle:** Common sense
- **Frequency:** Once

**Steps:** Start the server in an empty folder with a settings path that does not exist.
**Expected:** A clear notice that defaults were written, including the listening address.
**Actual:** The server starts on defaults (port 9000 on all interfaces) and writes a new file. Nothing in the first log lines calls this out.
**Suggested direction:** Log one line naming the created file and the address.

## Decisions

The owner reviewed the findings and asked for all four to be fixed.

| ID | Decision | Change |
|---|---|---|
| UT-001 | Fixed | `ServerEndpoints.DescribeAuth` and `xeno config` mask the key; the last four characters show for keys longer than eight |
| UT-002 | Fixed | `ClientConnectionState.Load` clears `IsBackground` when the recorded process has exited |
| UT-003 | Fixed | `WriteError` writes to stderr |
| UT-004 | Fixed | The server prints the created file and listening address |

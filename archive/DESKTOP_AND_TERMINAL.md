# Xeno Desktop, Terminal, and Tray Apps

The Xeno client runs as a background worker (`xeno start`) that connects this machine to the Xeno network. You can
manage it from five front ends. They all read and write the same `~/.xeno` files, so a change made in one shows up in
the others:

| Front end | Executable | Best for |
|---|---|---|
| Command line | `xeno` | Scripts, automation, and quick one-off changes. |
| Local web dashboard | served by the worker at `http://127.0.0.1:9343` | A browser view of this machine while the client runs. |
| Desktop app | `xeno-desktop` | A full native window on Windows, macOS, and Linux. |
| Terminal app | `xeno tui` | Everything the desktop app does, in a terminal or over SSH. |
| Tray agent | `xeno-tray` | Status at a glance, start and stop, and launching the other apps. |

The desktop app and the terminal app have the same pages, menus, forms, and settings. Neither one needs the worker
to be running: both read the configuration and the local history database directly, and they start and stop the worker
for you.

## Running from source

The scripts in [scripts/](../scripts/README.md) build and run each app. Use `scripts/linux/` on Linux and
`scripts\windows\*.bat` on Windows:

```bash
scripts/macos/run-desktop.sh     # desktop app (also starts the tray agent and connects)
scripts/macos/run-tui.sh         # terminal app in this terminal
scripts/macos/run-tray.sh        # tray agent
scripts/macos/run-local.sh tui   # local server + web dashboard + terminal app, isolated from ~/.xeno
scripts/macos/install.sh         # xeno tool, plus Xeno.app (macOS) or the Linux/Windows app folder
```

To run the apps by hand, build the solution once. The desktop app's output folder also contains `xeno` and
`xeno-tray`, so the three find each other:

```bash
dotnet build src/Xeno.sln
src/Xeno.Desktop/bin/Debug/net10.0/xeno-desktop
src/Xeno.Desktop/bin/Debug/net10.0/xeno tui
```

With the `xeno` .NET tool installed, run the terminal app with `xeno tui`.

The desktop app and the terminal app accept the same options:

| Option | Meaning |
|---|---|
| `--page <page>` | Open a page: `overview`, `models`, `providers`, `localendpoint`, `chat`, `chathistory`, `chatfeedback`, `usage`, `requests`, `logs`, `hosttelemetry`, `providertelemetry`, `server` (General), or `settings`. |
| `--server <cloud\|url\|host[:port]>` | Point this client at a Xeno server and save it. See [Choosing the Xeno server](#choosing-the-xeno-server). |
| `--home <dir>` | Use a different client home directory instead of `~/.xeno` (the same as setting `XENO_CLIENT_HOME`). |
| `--settings` | Open the Xeno Settings window at startup (desktop app only; the tray agent's **Settings** uses it). |

## Choosing the Xeno server

Every client app talks to one central Xeno server: the CLI, the worker, the desktop app, the terminal app, and the
tray agent. By default that is the **Xeno cloud** (`https://app.xenocloud.ai:9000`, WebSocket `wss://app.xenocloud.ai:9000/ws/v1/clients`). To use your own
server, for example one you run on your network or with `scripts/*/run-server.sh`, point the client at it in any of
these ways. They all save the same setting (`Server.Url`, and `Server.RestUrl` when set) in `~/.xeno/client.json`:

| From | How |
|---|---|
| Desktop app | **General** page (under Configuration), **Edit Connection**, then set the WebSocket URL (and optionally the REST URL). |
| Terminal app | **General** page (Alt+8), press `e`, then set the WebSocket URL. |
| Launching either app | `xeno-desktop --server <server>` or `xeno tui --server <server>`. |
| Command line | `xeno server use <server>`, then `xeno server show` to check. |
| Sign-in | `xeno login -u <user> --server <rest-url>` (the CLI) or the **Server** field of the Sign In form. |

`<server>` can be any of these:

- `cloud`: the Xeno cloud server, which is the default.
- `http://host:port` or `https://host`: the server's REST address. The WebSocket address
  (`ws://host:port/ws/v1/clients`, or `wss://` for `https`) is derived from it.
- `ws://host:port` or `wss://host`: the server's WebSocket address. If the path is missing, `/ws/v1/clients` is
  added.
- `host` or `host:port`: shorthand for `http://host:9000` or `http://host:port`.

If you paste a full URL, such as one copied from the browser's network tab, only the scheme, host, and port are used.
The Xeno cloud accepts only HTTPS and WSS. The apps upgrade an old `ws://` or `http://` cloud address saved in
`client.json` automatically.

```bash
xeno server use http://10.0.0.20:9000      # a server on your network
xeno-desktop --server https://xeno.example.com
xeno tui --server cloud                    # back to the Xeno cloud
```

The header of the terminal app and the status card of the desktop app show the server in use: **Xeno cloud**, or the
server's host and port. The Overview and General pages show the full URLs.

Things to know:

- A running client worker keeps its connection until it restarts. After changing servers, choose **Restart Client**
  (or run `xeno restart`). The apps remind you.
- Credentials belong to one server. After switching servers, sign in again on the **General** page.
- To switch between servers without signing in again each time, give each server its own client home. Every app
  accepts `--home`, and `XENO_CLIENT_HOME` works too:

  ```bash
  xeno-desktop --home ~/.xeno-lab --server http://lab-server:9000
  xeno tui --home ~/.xeno-lab
  ```

  `scripts/*/run-local.sh` works this way: it uses `~/.xeno-local` for the local server, so `~/.xeno` keeps its
  Xeno cloud sign-in.

## Pages

| Page | What it shows and does |
|---|---|
| Chat | Talk to any model on this machine or the Xeno network. Pick a conversation and a model (grouped by this machine and the Xeno network), type a message, and read the answer as it streams, with its route, timing, and tokens. Rate answers thumbs up or down with an optional comment, turn Xeno tools on for a conversation (tools that change something ask with Allow or Deny), set a system prompt and the working folder that file and process tools are confined to (Folder), rename, and delete. Needs the client running. In the terminal app: Enter sends (Send turns into Stop while answering), Ctrl+N starts a new chat, Esc stops an answer; Stream turns streaming on or off, Stats shows the last answer's telemetry, and Prompt sets the system prompt. |
| Chat history | Every chat turn from every Xeno app on this machine (web, desktop, terminal, CLI, MCP): totals, median and 95th percentile time, time to first token, the thumbs down share, a chart of turns by where they ran, a per-model table (desktop), and the turns themselves, filtered by range, route, and outcome. Open a turn for its details and tool calls. |
| Feedback | Thumbs up and down counts, the share of answers rated, and every rating with its comment, model, and conversation, filtered by range and rating. |
| Overview | Client status with Start, Stop, Restart, and Sign In; the local inference URL and routing mode (and, once, a notice when an upgrade moved the listener from port 11434 to 11435); requests served, failures, and latency; a requests-over-time chart with a range (last hour, day, week, or month); how many models are served here and offered by the network; local services; recent requests. |
| Models | One catalog of the models this machine serves and the models the Xeno network offers it, with where each is available (this machine, the network, or both). Add, edit, rename, move, and delete local models. **Import** (File > Import Models, or `i` in the terminal app) reads the live models of the provider endpoints you choose, lists each with a checkbox (models already advertised are marked and cannot be picked), lets you choose completions or embeddings per model (prefilled from the model's name), and advertises the picked ones; a provider that cannot be read shows its error in place. |
| Providers | Local inference endpoints (Ollama, vLLM, OpenAI-compatible), their models, and a connection test. Add, edit, and delete providers. **Manage Models** (the row menu or button in the desktop app, `p` in the terminal app; also on Local endpoint) shows a provider's live models with size, parameters, quantization, modified time, and whether each is advertised; pulls a model by name with a progress bar (optionally advertising it when it finishes), cancels a pull, and deletes a model from disk after confirming, which also removes its advertisement. Pulls keep running when the dialog closes and show in the status bar (desktop) or header (terminal) until they finish. Pull and delete work with Ollama providers; others are listed only. They work whether or not the provider lets the Xeno server manage models: that toggle on the provider form only decides whether the server may pull and delete models on this machine remotely. Each provider can be shared with the Xeno network or kept local-only (for example your own Ollama Cloud account), used for this machine's local endpoint requests, and given a concurrency limit; each model can have its own limit. |
| Local endpoint | The URL apps on this machine call, its routing mode, a traffic chart (answered here, sent to the network, fallbacks), each provider's health, latency, and load against its concurrency limit, the route rules, and recent local endpoint requests. Add, edit, and delete rules, test where a model would go, and check every provider now. In the terminal app: a add, e edit, d delete, t test a route, c check providers. |
| Usage | Requests, failures, latency, time to first token, streaming, throughput, tokens, a heatmap, and time by stage, for served requests, the local endpoint, or chat. Click a bucket to open the matching list. |
| Requests | Requests this machine served for the network, with a requests-over-time chart, range, outcome, and text filters, and full request details (timeline, headers, and stored bodies). Delete one request or clear the history. |
| Logs | The worker log or error log, with tail length, severity filter, and live follow. Routine server messages are rolled up (see below). |
| Host (Telemetry) | CPU, memory, disk, and GPU readings; a chart of the chosen metric over a range; stored samples; and Collect Now. |
| Provider (Telemetry) | Health, loaded and available models, queue depth, and throughput per provider; a chart of the chosen metric over a range; and stored samples. |
| General (Configuration) | The server connection, sign-in (username and password, or an access key), sign-out, connectivity checks, and what the network offers this tenant. |
| Settings | Appearance and every local setting: the web dashboard, the local inference server (port, routing mode, model discovery, provider health checks, and how long a local request waits for a free slot), the default concurrency limit per provider, request history retention, and telemetry. Saved to `client.json`; listener host and port changes apply after a restart, routing and concurrency changes apply right away. |

Create forms (Add Provider, Add Model, Add Route Rule) have **Save**, which saves and closes, and **Save and Add
Another**, which saves, confirms, clears the name, and keeps the provider and options for the next entry (Ctrl+N in
the terminal app). Every table has a **Columns** control (a button in the desktop table header; `[Columns]` on the
terminal table's frame, or `v`) to choose which columns it shows. Wide tables start with at most six columns, and each
table remembers its choice in `frontend.json`.

The sidebar groups pages under section headings (Client, Models, Chat, Activity, Telemetry, Configuration), so the
telemetry pages are just **Host** and **Provider** there. Menus, the command palette, and page headers use the full
names (Host Telemetry, Provider Telemetry).

### Charts

The desktop and terminal apps draw the same charts as the local web dashboard, built once in
`XenoCharts` (`Xeno.Client.Core`):

| Page | Charts |
|---|---|
| Overview and Requests | Requests over time: succeeded and failed requests per interval. |
| Usage | Every usage chart for the chosen dataset. See [USAGE_ANALYTICS.md](USAGE_ANALYTICS.md). |
| Host Telemetry | The chosen host metric over time: average, minimum, and maximum. |
| Provider Telemetry | The chosen provider metric over time, filtered by provider and model: average, minimum, and maximum. |

Every chart has a range (last hour, day, week, or month, opening on the last day), a heading such as "Requests, last day", a legend, its
peak value, and time labels. When there's no data, the chart says why instead of drawing an empty axis: for example,
no requests yet, telemetry is collected only while the client runs, or GPU telemetry needs an Apple silicon GPU (read from `ioreg`, no admin rights needed; utilization and unified memory in use, without temperature or power) or an NVIDIA GPU (`nvidia-smi` or DCGM). In the
desktop app, hover over a column to see its values.

Charts refresh on the **View > Auto Refresh** choice: off, every 15 seconds (the default), or every minute. An open
dialog pauses the refresh, and so does a list opened from a chart.

On the Usage page, click a bucket or move to it with the arrow keys and press Enter to open the matching list for that
window. In the terminal app the first click selects a bucket and the second opens it.

### Log rollup

The worker logs a debug line for every `server.telemetry.request` and `server.telemetry.ack` message, about once a
minute. On a lightly used machine those lines bury everything else, so the Logs page in both apps rolls up each
uninterrupted run of them into one line per message type with a count and the local time of the first, for example
`Server message: server.telemetry.ack (5 since 11:16:39)`. Any other line ends the run. The log file itself, `xeno logs`,
and the web dashboard still show every line.

## Desktop app

The window has a sidebar with the pages and a status card that starts, stops, or signs in, a page header with the
page's actions, and a status bar with the latest result. Signing in shows a confirmation that names the server and
tenant and offers **Start Client** (or **Restart Client** when the client is already running).

Menus:

- **macOS:** the menu bar shows **Xeno** (About Xeno, Settings), **File**, **Edit**, **View**, **Client**,
  **Window**, and **Help**, with standard Cmd shortcuts.
- **Windows and Linux:** the same File, View, Client, and Help menus appear at the top of the window and use Ctrl.

| Shortcut (Cmd on macOS, Ctrl elsewhere) | Action |
|---|---|
| 1 to 9 | Go to a page: Overview, Models, Providers, Requests, Logs, Host Telemetry, Provider Telemetry, General, Settings. |
| 0 | Go to the Local endpoint page. |
| N / Shift+N | Add a provider / add a model. |
| K | Command palette. |
| R | Refresh. |
| Shift+D | Open the local web dashboard. |
| Shift+T | Open the terminal app in a new terminal window. |
| , | Settings. |
| / | Keyboard shortcuts. |

Tables sort by clicking a column header. Double-click or press Enter to open a row, and right-click for row actions.

Only one desktop app runs per client home directory. Preferences (appearance, colors, last page) are stored in
`~/.xeno/frontend.json`.

### Colors

**Settings > Appearance** chooses light, dark, or system, and the colors. There are thirteen palettes: Xeno (the
original purple and green, the default), Graphite, Ocean, Arctic, Forest, Ember, Rose, Indigo, Sand, Lagoon, Citrus,
Paper (a light sidebar), and High Contrast. Each has a light and a dark version. **Custom** lets you pick six colors
for light and six for dark (background, panels, text, accent, sidebar, and sidebar highlight) with a swatch grid or a
hex value, starting from any palette. Secondary text, borders, button text, and status colors are worked out from
those so they stay readable, and the editor warns when a pair you chose is hard to read. Changes apply right away
and also color the tray agent the next time it starts.

The terminal app has the same palettes under **Settings > Colors**, chosen separately from the desktop app (as its
light or dark appearance is), and **Custom Colors** edits the same custom colors as hex values. The terminal keeps its
own background; a palette sets the text, accent, status, border, selection, and menu bar colors. The Xeno palette
keeps the terminal app's original look. The usage heatmap is colored from the same palette (TUIKit's
`HeatMapPalette`): empty cells in the muted color, then a ramp through the accent. The palettes are defined once in `XenoColorSchemes`
(`Xeno.Client.Core`), and a test checks every one against WCAG contrast minimums.

### Starting up

Every client app connects this computer when it starts: the desktop app, the terminal app, and the tray agent start
the client worker if you are signed in and it isn't already running (a sign-in reminder shows otherwise). Starts are
serialized across apps, so opening two at once starts one worker. Set `XENO_AUTOCONNECT=0` to turn this off.

Starting the desktop app, the terminal app, or the client from a command line (`xeno connect`, `xeno start`,
`xeno restart`, and so `run-client`) also starts the tray agent if it isn't running. That happens only when there is a
desktop to show the icon on: not over SSH, not under WSL, and on Linux only with `DISPLAY` or `WAYLAND_DISPLAY`. The
background worker never starts it, so quitting the agent sticks until you next open an app. Set
`XENO_AGENT_AUTOSTART=0` to never start it, or `1` to always start it.

## Terminal app

The terminal app (`xeno tui`) has a header, a menu bar, a navigation sidebar, the current page, a message line, and a
status bar that lists the keys that work in the focused area.

It is designed for a large terminal: it needs at least **120 columns by 36 rows**. In a smaller window it shows
TUIKit's "Terminal too small" screen with the size it needs, and returns as soon as the window is big enough.

The **message line**, above the key hints, reports everything the app does: a spinner while something runs (starting
the client, signing in, pinging the server, collecting telemetry), then the result with the time, marked ✓ for
success, ! for a warning, or ✗ for an error (errors also pop up a notice). After signing in, a confirmation names
the server and tenant and offers **Start Client** (or **Restart Client**).

Detail views (a request's details, provider models on Provider Telemetry, and Help > About) are Markdown laid out by
TUIKit's width-aware renderer in the app's theme colors: tables get borders and are sized from their content, headings and links are
styled, and headers and bodies sit in code blocks (JSON is highlighted). The Markdown is built in `Xeno.Client.Core`
with `XenoMarkdown`, which escapes values and fences bodies, so request and response content always shows verbatim.
Arrow keys, Page Up/Down, Home, End, and the mouse wheel scroll; Esc closes.

| Key | Action |
|---|---|
| F10 | Open the menu bar (arrow keys to move, Enter to choose, Esc to close). |
| Ctrl+K | Command palette. |
| F1 | Keyboard shortcuts, including each page's single-key actions. |
| Alt+1 to Alt+9, Alt+0 | Go to a page (the same numbers as the desktop app; Alt+0 is Local endpoint). Chat, Chat history, Feedback, and Usage have no number; use the sidebar, the menu, or Ctrl+K. |
| Tab / Shift+Tab | Move between the page's controls and the sidebar. |
| Enter | Open the selected row or page. |
| F5 | Refresh. |
| Ctrl+N | Add a provider. |
| Ctrl+Q, or Ctrl+C twice | Quit. |

The mouse works everywhere: click a menu title to open it and an item to run it (a click elsewhere closes the
menu), click the sidebar to change pages, click buttons, rows (double-click opens), checkboxes, and fields, click a
page dropdown (such as a chart range) to pick from a popup, click buttons in confirmations, rows in the command
palette, and the key hints in the status bar, and scroll lists, logs, and viewers with the wheel.

Each page also has single-letter actions that work when a text field isn't focused. For example, on Providers: `a`
add, `e` edit, `t` test, `m` add model, `p` manage models, `d` delete. On Models: `i` import. On Overview: `s` start or
stop, `r` restart, `w` web dashboard.
Forms move between fields with Tab, save with Ctrl+S (or the Save button), save and add another with Ctrl+N on
create forms, and cancel with Esc. Each field's help is shown under it, a
missing or malformed value is explained under its field, Ctrl+R shows a secret as typed, and Ctrl+A, Ctrl+E,
Ctrl+U, Ctrl+K, and Ctrl+W edit the line. A paste goes into the focused field, in a form or a page's filter box. The
Settings page groups its fields under the same section headings as the desktop app. A table says "Loading..." until
its page's data arrives, and shows the error in place of its rows if the first load fails.

**File > Open client.json** opens the settings file in `$VISUAL` or `$EDITOR` when one is set, and returns to the app
when the editor exits.

## Tray agent

The tray agent (`xeno-tray`, shown as **Xeno Agent**) lives in the macOS menu bar or the Windows and Linux
notification area. Its menu offers:

- The status line, which also shows the local endpoint address when the local inference server is enabled, for example `Xeno: Connected - local endpoint 127.0.0.1:11435, 2 of 2 providers healthy`
- Open Xeno Desktop (the desktop app)
- Open Xeno Terminal (the terminal app, in a new terminal window)
- Open Web Dashboard
- Settings (the desktop app's Xeno Settings window)
- Start Client, Stop Client, and Restart Client
- About Xeno Agent, and Quit Xeno, which quits all of Xeno: it stops the client (in the background or in a terminal from `run-client`), closes the desktop app and any terminal apps, then quits the agent

On Windows and Linux, clicking the icon opens the desktop app. On macOS, the agent has its own application menu and
shows in the Dock only while one of its windows is open. Only one agent runs per client home directory. Quitting the
agent doesn't stop the client.

## How the apps find each other

Each launcher looks for the executable in this order:

1. An environment variable: `XENO_CLI`, `XENO_DESKTOP`, or `XENO_TRAY` (a file or a folder).
2. The folder of the app that's launching it.
3. For `xeno` only: `PATH`, then `~/.dotnet/tools`.
4. On macOS: `/Applications/Xeno.app/Contents/MacOS` and `~/Applications/Xeno.app/Contents/MacOS`.
5. The newest build under `src/<Project>/bin` in a source checkout above the current folder.

The terminal app opens in a new window as follows:

- **macOS:** Terminal.app, using a temporary `.command` script (no automation permission is needed).
- **Windows:** a new `cmd.exe` window.
- **Linux:** the first terminal found among x-terminal-emulator, gnome-terminal, konsole, xfce4-terminal, kitty,
  alacritty, and xterm.

## Architecture

All five front ends share one client library, so behavior and validation stay identical everywhere:

| Project | Contents |
|---|---|
| `src/Xeno.Client.Core` | Settings and state (`client.json`, `state.json`), the local history database, telemetry, and the shared services: `XenoWorkspace` (the facade the desktop and terminal apps use), `ProviderConfiguration`, `ClientWorkerController` (start and stop), `ClientStatusSnapshot`, `ModelCatalogBuilder` (local plus network models), `XenoServerClient` (sign-in, ping, network model list), `ClientLogReader` and `ClientLogRollup`, `LocalSettingsEditor`, and `XenoAppLauncher` with `XenoExecutableLocator` and `TerminalLaunchPlanner`. The `Presentation` namespace holds what the UIs render: the navigation catalog, the menu layout, command availability, form specs, the settings catalog, charts (`XenoCharts`), follow-ups after actions (`XenoClientActions.AfterSignIn`), and table rows. |
| `src/Xeno.Client` | The `xeno` CLI, the background worker, the local web dashboard, and the local inference server. It hosts the terminal app as `xeno tui`. |
| `src/Xeno.Desktop.Common` | Avalonia building blocks shared by the desktop app and the tray agent: the theme (which applies a `XenoColorSchemes` palette), native menus built from the shared layout, the About window, dialogs, the form dialog, and tables. |
| `src/Xeno.Desktop` | The desktop app. |
| `src/Xeno.Tray` | The tray agent. |
| `src/Xeno.Tui` | The terminal app, built on TUIKit. |

Two things in the shared library prepare for routing each model either locally or through the network:

- `ModelCatalog` already records, for every model, whether this machine serves it, whether the network offers it, or
  both.
- The desktop and terminal Models pages show that view.

## Testing

The shared library, the tray and desktop logic, and the terminal app are covered by Touchstone suites in
`src/Test.Shared`: `SharedClient`, `Tray`, `Desktop`, and `Tui`. The terminal app tests run the real app on TUIKit's
headless backend and assert on rendered frames. Set `XENO_TUI_SNAPSHOT_DIR` to a folder to save each page's frame at
80x24 and 140x45 while the `Tui` suite runs:

```bash
dotnet run --project src/Test.Automated -- --suite Tui
```

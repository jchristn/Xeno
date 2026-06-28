# Xeno.DataLoader

`Xeno.DataLoader` is a console application for loading synthetic Xeno dashboard data. It is intended for two local-only use cases:

- Building dashboard screenshots with enough volume, client variety, and plausible labels to make charts and tables look occupied.
- Testing dashboard behavior across dense telemetry, sparse telemetry, errors, degraded providers, traffic spikes, and mixed accelerator capacity.

The loader creates synthetic clients, request history, host telemetry, provider telemetry, provider model/metric rows, and collection attempts. Generated clients are marked with the `xcl_demo_` prefix, and generated request history is marked with `req_demo_` plus telemetry metadata so repeated runs can replace prior loader data without deleting real local client records.

## Example

```powershell
dotnet run --project src/Xeno.DataLoader -- `
  --from 2026-05-01T00:00:00Z `
  --to 2026-06-01T00:00:00Z `
  --clients 36 `
  --randomness 0.85 `
  --density 45 `
  --scenario mixed `
  --filename docker/data/xeno.db
```

## Options

- `--from` and `--to`: UTC time range to populate.
- `--days`: convenience alternative to `--from`; uses `--to` minus this many days.
- `--clients`: number of synthetic clients to simulate.
- `--randomness`: `0` to `1`; higher values increase spikes, gaps, variance, queue buildup, and outliers.
- `--density`: `0` to `60`; telemetry samples per hour per synthetic client.
- `--scenario`: `screenshot`, `testing`, or `mixed`.
- `--database-type`: currently `Sqlite` for full request and telemetry loading.
- `--filename`: SQLite database file. Defaults to `docker/data/xeno.db`.
- `--seed`: integer seed for repeatable synthetic data.
- `--replace-synthetic`: defaults to `true`.
- `--backup`: defaults to `true`.

For the Docker deployment, stop the server before loading data and restart it afterward so platform-state request history is reloaded into memory.

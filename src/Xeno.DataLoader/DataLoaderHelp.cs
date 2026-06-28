namespace Xeno.DataLoader
{
    internal static class DataLoaderHelp
    {
        public const string Text = """
Xeno.DataLoader

Loads synthetic clients, request history, and client telemetry into a Xeno database for dashboard screenshots and dashboard testing.

Usage:
  dotnet run --project src/Xeno.DataLoader -- --from 2026-05-01T00:00:00Z --to 2026-06-01T00:00:00Z --clients 24 --randomness 0.75 --density 30 --filename docker/data/xeno.db

Required concepts:
  --from                  Start timestamp for synthetic data, ISO-8601 UTC.
  --to                    End timestamp for synthetic data, ISO-8601 UTC.
  --clients               Number of synthetic clients to simulate. Minimum 1.
  --randomness            0 to 1. Lower values follow realistic patterns; higher values create more variance, spikes, gaps, and outliers.
  --density               0 to 60 telemetry samples per hour per synthetic client.
  --database-type         Database provider. Sqlite is supported for full request and telemetry loading.
  --filename              SQLite database filename. Defaults to docker/data/xeno.db.

Optional:
  --days                  Convenience alternative to --from; uses --to minus this many days.
  --connection-string     Provider-specific connection string.
  --tenant-id             Tenant to seed. Defaults to ten_default.
  --scenario              screenshot, testing, or mixed. Defaults to screenshot.
  --seed                  Integer seed for repeatable data generation.
  --replace-synthetic     true or false. Defaults to true, replacing prior loader-created synthetic data.
  --backup                true or false. Defaults to true for SQLite.
  --help                  Show this help.

Scenario intent:
  screenshot              Balanced, human-readable data with day/night patterns and believable client/provider names.
  testing                 More edge cases: errors, sparse periods, spikes, provider degradation, and uneven client behavior.
  mixed                   Screenshot-friendly baseline with a moderate number of test-like edge cases.
""";
    }
}

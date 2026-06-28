namespace Xeno.DataLoader
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using Xeno.Core.Enums;

    internal static class DataLoaderOptionsParser
    {
        public static DataLoaderOptions Parse(string[] args, DateTime defaultNowUtc)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));

            DataLoaderOptions options = new DataLoaderOptions
            {
                FromUtc = defaultNowUtc.AddDays(-31),
                ToUtc = defaultNowUtc
            };

            Dictionary<string, string?> values = ReadArguments(args);
            if (values.ContainsKey("help") || values.ContainsKey("h") || values.ContainsKey("?"))
            {
                options.ShowHelp = true;
                return options;
            }

            if (TryGet(values, "from", out string? fromValue)) options.FromUtc = ParseUtc(fromValue, "from");
            if (TryGet(values, "to", out string? toValue)) options.ToUtc = ParseUtc(toValue, "to");
            if (TryGet(values, "days", out string? daysValue)) options.FromUtc = options.ToUtc.AddDays(-ParseInt(daysValue, "days"));
            if (TryGet(values, "clients", out string? clientsValue)) options.ClientCount = ParseInt(clientsValue, "clients");
            if (TryGet(values, "randomness", out string? randomnessValue)) options.Randomness = ParseDouble(randomnessValue, "randomness");
            if (TryGet(values, "density", out string? densityValue)) options.TelemetrySamplesPerHour = ParseInt(densityValue, "density");
            if (TryGet(values, "database-type", out string? databaseTypeValue)) options.DatabaseType = ParseDatabaseType(databaseTypeValue);
            if (TryGet(values, "filename", out string? filenameValue)) options.Filename = filenameValue;
            if (TryGet(values, "connection-string", out string? connectionStringValue)) options.ConnectionString = connectionStringValue;
            if (TryGet(values, "tenant-id", out string? tenantIdValue)) options.TenantId = RequireValue(tenantIdValue, "tenant-id");
            if (TryGet(values, "scenario", out string? scenarioValue)) options.Scenario = NormalizeScenario(scenarioValue);
            if (TryGet(values, "seed", out string? seedValue)) options.Seed = ParseInt(seedValue, "seed");
            if (TryGet(values, "replace-synthetic", out string? replaceValue)) options.ReplaceSynthetic = ParseBool(replaceValue, "replace-synthetic");
            if (TryGet(values, "backup", out string? backupValue)) options.Backup = ParseBool(backupValue, "backup");

            if (options.ToUtc <= options.FromUtc)
            {
                throw new ArgumentException("The --to value must be later than the --from value.");
            }

            if (String.IsNullOrWhiteSpace(options.TenantId))
            {
                throw new ArgumentException("The --tenant-id value must not be empty.");
            }

            return options;
        }

        private static Dictionary<string, string?> ReadArguments(string[] args)
        {
            Dictionary<string, string?> values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            int index = 0;
            while (index < args.Length)
            {
                string token = args[index];
                if (!token.StartsWith("--", StringComparison.Ordinal) && !token.StartsWith("-", StringComparison.Ordinal))
                {
                    throw new ArgumentException("Unexpected positional argument: " + token);
                }

                string name = token.TrimStart('-');
                string? value = null;
                int equalsIndex = name.IndexOf('=', StringComparison.Ordinal);
                if (equalsIndex >= 0)
                {
                    value = name.Substring(equalsIndex + 1);
                    name = name.Substring(0, equalsIndex);
                }
                else if (index + 1 < args.Length && !args[index + 1].StartsWith("-", StringComparison.Ordinal))
                {
                    index++;
                    value = args[index];
                }

                values[name] = value;
                index++;
            }

            return values;
        }

        private static bool TryGet(Dictionary<string, string?> values, string name, out string? value)
        {
            return values.TryGetValue(name, out value);
        }

        private static DateTime ParseUtc(string? value, string name)
        {
            string required = RequireValue(value, name);
            if (!DateTime.TryParse(required, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime parsed))
            {
                throw new ArgumentException("Invalid --" + name + " value. Use an ISO-8601 UTC timestamp.");
            }

            return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
        }

        private static int ParseInt(string? value, string name)
        {
            string required = RequireValue(value, name);
            if (!Int32.TryParse(required, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                throw new ArgumentException("Invalid --" + name + " value. Expected an integer.");
            }

            return parsed;
        }

        private static double ParseDouble(string? value, string name)
        {
            string required = RequireValue(value, name);
            if (!Double.TryParse(required, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
            {
                throw new ArgumentException("Invalid --" + name + " value. Expected a number.");
            }

            return parsed;
        }

        private static bool ParseBool(string? value, string name)
        {
            string required = RequireValue(value, name);
            if (!Boolean.TryParse(required, out bool parsed))
            {
                throw new ArgumentException("Invalid --" + name + " value. Expected true or false.");
            }

            return parsed;
        }

        private static DatabaseTypeEnum ParseDatabaseType(string? value)
        {
            string required = RequireValue(value, "database-type");
            if (Enum.TryParse(required, true, out DatabaseTypeEnum parsed)) return parsed;
            throw new ArgumentException("Invalid --database-type value. Supported value for synthetic telemetry loading is Sqlite.");
        }

        private static string NormalizeScenario(string? value)
        {
            string required = RequireValue(value, "scenario").Trim().ToLowerInvariant();
            if (required == "screenshot" || required == "testing" || required == "mixed") return required;
            throw new ArgumentException("Invalid --scenario value. Expected screenshot, testing, or mixed.");
        }

        private static string RequireValue(string? value, string name)
        {
            if (String.IsNullOrWhiteSpace(value)) throw new ArgumentException("Missing value for --" + name + ".");
            return value;
        }
    }
}

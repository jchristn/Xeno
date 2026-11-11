namespace Xeno.Core.Analytics
{
    using System;
    using System.Globalization;

    /// <summary>
    /// Reads usage analytics and provider health queries from query-string values, the same way on the server and the
    /// local dashboard. Bad values are an <see cref="ArgumentException"/> (a 400), never a silent default.
    /// </summary>
    public static class UsageQueryParser
    {
        /// <summary>
        /// Parse a usage analytics query.
        /// </summary>
        /// <param name="get">Returns a query-string value by name, or null.</param>
        /// <returns>Query.</returns>
        /// <exception cref="ArgumentNullException">get is null.</exception>
        /// <exception cref="ArgumentException">A value is not valid.</exception>
        public static UsageAnalyticsQuery Parse(Func<string, string?> get)
        {
            ArgumentNullException.ThrowIfNull(get);
            DateTime to = Date(get("toUtc"), "toUtc") ?? DateTime.UtcNow;
            DateTime from = Date(get("fromUtc"), "fromUtc") ?? to.AddDays(-1);
            int bucketMinutes = Int(get("bucketMinutes"), "bucketMinutes") ?? UsageRanges.DefaultBucketMinutes(from, to);
            UsageRanges.Validate(from, to, bucketMinutes);
            UsageAnalyticsQuery query = new UsageAnalyticsQuery
            {
                FromUtc = from,
                ToUtc = to,
                BucketMinutes = bucketMinutes,
                Model = get("model"),
                Provider = get("provider"),
                ClientId = get("clientId"),
                UserId = get("userId"),
                CredentialId = get("credentialId"),
                TenantId = get("tenantId"),
                GroupBy = Enum<UsageGroupByEnum>(get("groupBy"), "groupBy") ?? UsageGroupByEnum.Model,
                GroupMetric = Enum<UsageGroupMetricEnum>(get("groupMetric"), "groupMetric") ?? UsageGroupMetricEnum.Requests,
                TopN = Int(get("topN"), "topN"),
                UtcOffsetMinutes = Int(get("utcOffsetMinutes"), "utcOffsetMinutes") ?? 0
            };

            string? sections = get("sections");
            if (!String.IsNullOrWhiteSpace(sections))
            {
                UsageSectionsEnum flags = UsageSectionsEnum.None;
                foreach (string part in sections.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (!System.Enum.TryParse(part, true, out UsageSectionsEnum value) || int.TryParse(part, out _)) throw new ArgumentException("Unknown section '" + part + "'.");
                    flags |= value;
                }

                query.Sections = flags;
            }

            return query;
        }

        /// <summary>
        /// Parse a provider health query.
        /// </summary>
        /// <param name="get">Returns a query-string value by name, or null.</param>
        /// <returns>Query.</returns>
        /// <exception cref="ArgumentNullException">get is null.</exception>
        /// <exception cref="ArgumentException">A value is not valid.</exception>
        public static ProviderHealthQuery ParseHealth(Func<string, string?> get)
        {
            ArgumentNullException.ThrowIfNull(get);
            DateTime to = Date(get("toUtc"), "toUtc") ?? DateTime.UtcNow;
            DateTime from = Date(get("fromUtc"), "fromUtc") ?? to.AddDays(-1);
            int bucketMinutes = Int(get("bucketMinutes"), "bucketMinutes") ?? UsageRanges.DefaultBucketMinutes(from, to);
            UsageRanges.Validate(from, to, bucketMinutes);
            return new ProviderHealthQuery { FromUtc = from, ToUtc = to, BucketMinutes = bucketMinutes, ProviderName = get("providerName"), ClientId = get("clientId") };
        }

        /// <summary>
        /// Query string for a usage analytics query, without the leading question mark.
        /// </summary>
        /// <param name="query">Query.</param>
        /// <returns>Query string.</returns>
        /// <exception cref="ArgumentNullException">query is null.</exception>
        public static string ToQueryString(UsageAnalyticsQuery query)
        {
            ArgumentNullException.ThrowIfNull(query);
            System.Collections.Generic.List<string> parts = new System.Collections.Generic.List<string>
            {
                "fromUtc=" + Uri.EscapeDataString(query.FromUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)),
                "toUtc=" + Uri.EscapeDataString(query.ToUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)),
                "bucketMinutes=" + query.BucketMinutes.ToString(CultureInfo.InvariantCulture),
                "groupBy=" + query.GroupBy,
                "groupMetric=" + query.GroupMetric,
                "utcOffsetMinutes=" + query.UtcOffsetMinutes.ToString(CultureInfo.InvariantCulture)
            };
            Add(parts, "model", query.Model);
            Add(parts, "provider", query.Provider);
            Add(parts, "clientId", query.ClientId);
            Add(parts, "userId", query.UserId);
            Add(parts, "credentialId", query.CredentialId);
            Add(parts, "tenantId", query.TenantId);
            if (query.TopN.HasValue) parts.Add("topN=" + query.TopN.Value.ToString(CultureInfo.InvariantCulture));
            if (query.Sections != UsageSectionsEnum.All) parts.Add("sections=" + Uri.EscapeDataString(query.Sections.ToString().Replace(" ", String.Empty, StringComparison.Ordinal)));
            return String.Join("&", parts);
        }

        /// <summary>
        /// Query string for a provider health query, without the leading question mark.
        /// </summary>
        /// <param name="query">Query.</param>
        /// <returns>Query string.</returns>
        /// <exception cref="ArgumentNullException">query is null.</exception>
        public static string ToQueryString(ProviderHealthQuery query)
        {
            ArgumentNullException.ThrowIfNull(query);
            System.Collections.Generic.List<string> parts = new System.Collections.Generic.List<string>
            {
                "fromUtc=" + Uri.EscapeDataString(query.FromUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)),
                "toUtc=" + Uri.EscapeDataString(query.ToUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)),
                "bucketMinutes=" + query.BucketMinutes.ToString(CultureInfo.InvariantCulture)
            };
            Add(parts, "providerName", query.ProviderName);
            Add(parts, "clientId", query.ClientId);
            return String.Join("&", parts);
        }

        private static void Add(System.Collections.Generic.List<string> parts, string name, string? value)
        {
            if (!String.IsNullOrWhiteSpace(value)) parts.Add(name + "=" + Uri.EscapeDataString(value));
        }

        private static DateTime? Date(string? value, string name)
        {
            if (String.IsNullOrWhiteSpace(value)) return null;
            if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime parsed)) throw new ArgumentException(name + " is not a valid date.");
            return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
        }

        private static int? Int(string? value, string name)
        {
            if (String.IsNullOrWhiteSpace(value)) return null;
            if (!Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) throw new ArgumentException(name + " must be a whole number.");
            return parsed;
        }

        private static T? Enum<T>(string? value, string name) where T : struct, System.Enum
        {
            if (String.IsNullOrWhiteSpace(value)) return null;
            if (int.TryParse(value, out _) || !System.Enum.TryParse(value, true, out T parsed)) throw new ArgumentException(name + " '" + value + "' is not one of: " + String.Join(", ", System.Enum.GetNames(typeof(T))) + ".");
            return parsed;
        }
    }
}

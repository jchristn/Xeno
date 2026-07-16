namespace Xeno.Core.Helpers
{
    using System;

    /// <summary>
    /// PrettyId-backed identifier generator.
    /// </summary>
    public static class IdGenerator
    {
        /// <summary>
        /// Generated ID length, including prefix.
        /// </summary>
        public const int MaxIdLength = 32;

        private static readonly PrettyId.IdGenerator _Generator = new PrettyId.IdGenerator();

        /// <summary>
        /// Generate an ID with the supplied prefix.
        /// </summary>
        /// <param name="prefix">Identifier prefix.</param>
        /// <returns>K-sortable identifier.</returns>
        public static string NewId(string prefix)
        {
            if (prefix == null) throw new ArgumentNullException(nameof(prefix));

            string normalizedPrefix = NormalizePrefix(prefix);
            string id = _Generator.GenerateKSortable(normalizedPrefix, MaxIdLength);
            if (id.Length != MaxIdLength)
            {
                throw new InvalidOperationException("Generated PrettyId length mismatch.");
            }

            return id;
        }

        /// <summary>
        /// Generate a request history identifier.
        /// </summary>
        /// <returns>Request history identifier.</returns>
        public static string NewRequestHistoryId()
        {
            return NewId("req_");
        }

        /// <summary>
        /// Generate a server host telemetry sample identifier.
        /// </summary>
        /// <returns>Telemetry sample identifier.</returns>
        public static string NewClientTelemetrySampleId()
        {
            return NewId("ctel_");
        }

        /// <summary>
        /// Generate a server GPU telemetry row identifier.
        /// </summary>
        /// <returns>GPU telemetry row identifier.</returns>
        public static string NewClientTelemetryGpuSampleId()
        {
            return NewId("ctgpu_");
        }

        /// <summary>
        /// Generate a server provider telemetry sample identifier.
        /// </summary>
        /// <returns>Provider telemetry sample identifier.</returns>
        public static string NewClientProviderTelemetrySampleId()
        {
            return NewId("cptel_");
        }

        /// <summary>
        /// Generate a server provider model telemetry row identifier.
        /// </summary>
        /// <returns>Provider model telemetry row identifier.</returns>
        public static string NewClientProviderTelemetryModelSampleId()
        {
            return NewId("cptmdl_");
        }

        /// <summary>
        /// Generate a server provider metric telemetry row identifier.
        /// </summary>
        /// <returns>Provider metric telemetry row identifier.</returns>
        public static string NewClientProviderTelemetryMetricSampleId()
        {
            return NewId("cptmet_");
        }

        /// <summary>
        /// Generate a telemetry collection request or attempt identifier.
        /// </summary>
        /// <returns>Telemetry collection request identifier.</returns>
        public static string NewTelemetryCollectionRequestId()
        {
            return NewId("telreq_");
        }

        /// <summary>
        /// Generate a ledger tenant link identifier.
        /// </summary>
        /// <returns>Ledger tenant link identifier.</returns>
        public static string NewLedgerTenantLinkId()
        {
            return NewId("ledten_");
        }

        /// <summary>
        /// Generate a ledger account link identifier.
        /// </summary>
        /// <returns>Ledger account link identifier.</returns>
        public static string NewLedgerAccountLinkId()
        {
            return NewId("ledacct_");
        }

        /// <summary>
        /// Generate a ledger event identifier.
        /// </summary>
        /// <returns>Ledger event identifier.</returns>
        public static string NewLedgerEventId()
        {
            return NewId("ledevt_");
        }

        private static string NormalizePrefix(string prefix)
        {
            if (String.IsNullOrWhiteSpace(prefix)) return String.Empty;
            return prefix.EndsWith("_", StringComparison.Ordinal) ? prefix : prefix + "_";
        }
    }
}

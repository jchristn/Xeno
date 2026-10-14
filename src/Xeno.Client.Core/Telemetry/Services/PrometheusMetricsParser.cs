namespace Xeno.Client.Telemetry.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading;
    using Xeno.Client.Telemetry.Abstractions;
    using Xeno.Client.Telemetry.Models;

    /// <summary>
    /// Prometheus text exposition parser.
    /// </summary>
    public class PrometheusMetricsParser : IPrometheusMetricsParser
    {
        /// <inheritdoc />
        public PrometheusMetricDocument Parse(string metrics, CancellationToken token)
        {
            PrometheusMetricDocument document = new PrometheusMetricDocument();
            if (String.IsNullOrWhiteSpace(metrics)) return document;
            string[] lines = metrics.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (string line in lines)
            {
                token.ThrowIfCancellationRequested();
                if (String.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal)) continue;
                PrometheusMetricSample? sample = ParseLine(line);
                if (sample != null) document.Samples.Add(sample);
            }

            return document;
        }

        private static PrometheusMetricSample? ParseLine(string line)
        {
            string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length < 2) return null;
            if (!Double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double value)) return null;
            long? timestamp = null;
            if (parts.Length > 2 && Int64.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsedTimestamp))
            {
                timestamp = parsedTimestamp;
            }

            string nameAndLabels = parts[0];
            string name = nameAndLabels;
            List<ClientProviderTelemetryLabel> labels = new List<ClientProviderTelemetryLabel>();
            int braceStart = nameAndLabels.IndexOf('{');
            if (braceStart >= 0)
            {
                name = nameAndLabels.Substring(0, braceStart);
                int braceEnd = nameAndLabels.LastIndexOf('}');
                if (braceEnd > braceStart)
                {
                    labels = ParseLabels(nameAndLabels.Substring(braceStart + 1, braceEnd - braceStart - 1));
                }
            }

            return new PrometheusMetricSample
            {
                Name = name,
                Value = value,
                Timestamp = timestamp,
                Labels = labels
            };
        }

        private static List<ClientProviderTelemetryLabel> ParseLabels(string input)
        {
            List<ClientProviderTelemetryLabel> labels = new List<ClientProviderTelemetryLabel>();
            List<string> segments = SplitLabels(input);
            foreach (string segment in segments)
            {
                string[] pair = segment.Split('=', 2, StringSplitOptions.TrimEntries);
                if (pair.Length != 2) continue;
                labels.Add(new ClientProviderTelemetryLabel
                {
                    Name = pair[0].Trim(),
                    Value = pair[1].Trim().Trim('"').Replace("\\\"", "\"", StringComparison.Ordinal)
                });
            }

            return labels;
        }

        private static List<string> SplitLabels(string input)
        {
            List<string> segments = new List<string>();
            bool inQuotes = false;
            bool escaped = false;
            int start = 0;
            for (int index = 0; index < input.Length; index++)
            {
                char current = input[index];
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (current == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (current == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }

                if (current == ',' && !inQuotes)
                {
                    segments.Add(input.Substring(start, index - start));
                    start = index + 1;
                }
            }

            segments.Add(input.Substring(start));
            return segments;
        }
    }
}

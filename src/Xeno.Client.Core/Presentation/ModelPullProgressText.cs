namespace Xeno.Client.Presentation
{
    using System;
    using System.Globalization;
    using System.Text;
    using Xeno.Client.Services;

    /// <summary>
    /// Text for a model pull's progress: a single-line bar for terminals that redraw in place, and a plain line for logs
    /// and redirected output.
    /// </summary>
    public static class ModelPullProgressText
    {
        /// <summary>
        /// Narrowest bar drawn.
        /// </summary>
        public const int MinimumBarWidth = 10;

        /// <summary>
        /// Widest bar drawn.
        /// </summary>
        public const int MaximumBarWidth = 40;

        /// <summary>
        /// Percent, bytes, and status, for example "45.2%  1.2 GB / 2.7 GB  pulling 6a0746a1ec1a".
        /// </summary>
        /// <param name="job">Job snapshot.</param>
        /// <returns>Text.</returns>
        /// <exception cref="ArgumentNullException">Job is null.</exception>
        public static string Summary(ModelPullJob job)
        {
            ArgumentNullException.ThrowIfNull(job);
            StringBuilder builder = new StringBuilder();
            builder.Append(job.Percent.HasValue ? job.Percent.Value.ToString("0.0", CultureInfo.InvariantCulture).PadLeft(5) + "%" : "    -%");
            if (job.TotalBytes > 0) builder.Append("  ").Append(DisplayFormat.Bytes(job.CompletedBytes)).Append(" / ").Append(DisplayFormat.Bytes(job.TotalBytes));
            if (!String.IsNullOrWhiteSpace(job.Status)) builder.Append("  ").Append(ShortStatus(job.Status));
            return builder.ToString();
        }

        /// <summary>
        /// A plain progress line with the model name, for output that is not a terminal.
        /// </summary>
        /// <param name="job">Job snapshot.</param>
        /// <returns>Text.</returns>
        /// <exception cref="ArgumentNullException">Job is null.</exception>
        public static string PlainLine(ModelPullJob job)
        {
            ArgumentNullException.ThrowIfNull(job);
            return job.Model + ": " + Summary(job).TrimStart();
        }

        /// <summary>
        /// A bar line fitted to a terminal width, for example "[########------------]  45.2%  1.2 GB / 2.7 GB  pulling ...".
        /// The result is never longer than <paramref name="width"/> minus one, so redrawing it with a carriage return never wraps.
        /// </summary>
        /// <param name="job">Job snapshot.</param>
        /// <param name="width">Terminal width in columns.</param>
        /// <returns>Text.</returns>
        /// <exception cref="ArgumentNullException">Job is null.</exception>
        public static string BarLine(ModelPullJob job, int width)
        {
            ArgumentNullException.ThrowIfNull(job);
            int available = Math.Max(20, width) - 1;
            string summary = Summary(job);
            int barWidth = Math.Clamp(available - summary.Length - 4, MinimumBarWidth, MaximumBarWidth);
            double fraction = Math.Clamp((job.Percent ?? 0) / 100.0, 0, 1);
            int filled = (int)Math.Round(fraction * barWidth, MidpointRounding.AwayFromZero);
            string line = "[" + new string('#', filled) + new string('-', barWidth - filled) + "] " + summary;
            return line.Length > available ? DisplayFormat.Truncate(line, available) : line;
        }

        private static string ShortStatus(string status)
        {
            // Ollama reports "pulling sha256:<64 hex>"; the first twelve digest characters are enough to tell layers apart.
            const string prefix = "pulling sha256:";
            if (status.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && status.Length > prefix.Length + 12)
            {
                return "pulling " + status.Substring(prefix.Length, 12);
            }

            return status;
        }
    }
}

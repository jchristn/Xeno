namespace Xeno.Client.Presentation
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Colors for categorical chart series (models, providers, stages), shared by every surface through the chart
    /// catalog. Status tones (success, danger, warning, info, accent) come from each surface's theme instead.
    /// The eight colors are distinguishable in light and dark themes and avoid pure red and green, which mean failure
    /// and success.
    /// </summary>
    public static class XenoChartPalette
    {
        /// <summary>
        /// Hex colors for <see cref="XenoChartToneEnum.Series1"/> through <see cref="XenoChartToneEnum.Series8"/>.
        /// </summary>
        public static readonly IReadOnlyList<string> Categories = new List<string> { "#3B82F6", "#F59E0B", "#14B8A6", "#8B5CF6", "#EC4899", "#06B6D4", "#84CC16", "#F97316" };

        /// <summary>
        /// Hex color for <see cref="XenoChartToneEnum.Neutral"/> (Other, Unknown, no data).
        /// </summary>
        public const string Neutral = "#9CA3AF";

        /// <summary>
        /// Hex color for a categorical or neutral tone, or null for theme tones.
        /// </summary>
        /// <param name="tone">Tone.</param>
        /// <returns>Hex color, or null.</returns>
        public static string? Hex(XenoChartToneEnum tone)
        {
            if (tone == XenoChartToneEnum.Neutral) return Neutral;
            int index = (int)tone - (int)XenoChartToneEnum.Series1;
            return index >= 0 && index < Categories.Count ? Categories[index] : null;
        }

        /// <summary>
        /// Red, green, and blue bytes of a hex color.
        /// </summary>
        /// <param name="hex">Color such as #3B82F6.</param>
        /// <returns>Three bytes.</returns>
        /// <exception cref="ArgumentException">The color is not #RRGGBB.</exception>
        public static byte[] Rgb(string hex)
        {
            if (String.IsNullOrWhiteSpace(hex) || hex.Length != 7 || hex[0] != '#') throw new ArgumentException("Expected #RRGGBB.", nameof(hex));
            return new[] { Convert.ToByte(hex.Substring(1, 2), 16), Convert.ToByte(hex.Substring(3, 2), 16), Convert.ToByte(hex.Substring(5, 2), 16) };
        }
    }
}

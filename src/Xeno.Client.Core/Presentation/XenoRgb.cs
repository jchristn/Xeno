namespace Xeno.Client.Presentation
{
    using System;
    using System.Globalization;

    /// <summary>
    /// An opaque sRGB color with the arithmetic the color schemes need: parsing #rrggbb, mixing, relative luminance, and
    /// WCAG contrast. Immutable and thread safe.
    /// </summary>
    public sealed class XenoRgb
    {
        #region Public-Members

        /// <summary>
        /// Red, 0 to 255.
        /// </summary>
        public int R { get; }

        /// <summary>
        /// Green, 0 to 255.
        /// </summary>
        public int G { get; }

        /// <summary>
        /// Blue, 0 to 255.
        /// </summary>
        public int B { get; }

        /// <summary>
        /// White.
        /// </summary>
        public static XenoRgb White { get; } = new XenoRgb(255, 255, 255);

        /// <summary>
        /// Black.
        /// </summary>
        public static XenoRgb Black { get; } = new XenoRgb(0, 0, 0);

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create a color. Channels are clamped to 0 through 255.
        /// </summary>
        /// <param name="r">Red.</param>
        /// <param name="g">Green.</param>
        /// <param name="b">Blue.</param>
        public XenoRgb(int r, int g, int b)
        {
            R = Math.Clamp(r, 0, 255);
            G = Math.Clamp(g, 0, 255);
            B = Math.Clamp(b, 0, 255);
        }

        /// <summary>
        /// Parse #rrggbb, rrggbb, #rgb, or rgb (case insensitive, surrounding spaces allowed).
        /// </summary>
        /// <param name="value">Text.</param>
        /// <returns>Color, or null when the text is not a color.</returns>
        public static XenoRgb? TryParse(string? value)
        {
            if (String.IsNullOrWhiteSpace(value)) return null;
            string text = value.Trim();
            if (text.StartsWith("#", StringComparison.Ordinal)) text = text.Substring(1);
            if (text.Length == 3) text = new string(new[] { text[0], text[0], text[1], text[1], text[2], text[2] });
            if (text.Length != 6) return null;
            if (!Int32.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int packed)) return null;
            return new XenoRgb((packed >> 16) & 0xff, (packed >> 8) & 0xff, packed & 0xff);
        }

        /// <summary>
        /// Parse a color that must be valid.
        /// </summary>
        /// <param name="value">Text.</param>
        /// <returns>Color.</returns>
        /// <exception cref="FormatException">The text is not a color.</exception>
        public static XenoRgb Parse(string value)
        {
            return TryParse(value) ?? throw new FormatException("Not a color: " + value);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Lowercase #rrggbb.
        /// </summary>
        /// <returns>Hex text.</returns>
        public string ToHex()
        {
            return "#" + R.ToString("x2", CultureInfo.InvariantCulture) + G.ToString("x2", CultureInfo.InvariantCulture) + B.ToString("x2", CultureInfo.InvariantCulture);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return ToHex();
        }

        /// <summary>
        /// Blend toward another color.
        /// </summary>
        /// <param name="other">Other color.</param>
        /// <param name="amount">0 keeps this color, 1 gives the other.</param>
        /// <returns>Blended color.</returns>
        public XenoRgb Mix(XenoRgb other, double amount)
        {
            ArgumentNullException.ThrowIfNull(other);
            double t = Math.Clamp(amount, 0, 1);
            return new XenoRgb((int)Math.Round(R + (other.R - R) * t), (int)Math.Round(G + (other.G - G) * t), (int)Math.Round(B + (other.B - B) * t));
        }

        /// <summary>
        /// WCAG relative luminance, 0 (black) to 1 (white).
        /// </summary>
        /// <returns>Luminance.</returns>
        public double Luminance()
        {
            return 0.2126 * Linear(R) + 0.7152 * Linear(G) + 0.0722 * Linear(B);
        }

        /// <summary>
        /// WCAG contrast ratio with another color, 1 to 21.
        /// </summary>
        /// <param name="other">Other color.</param>
        /// <returns>Ratio.</returns>
        public double Contrast(XenoRgb other)
        {
            ArgumentNullException.ThrowIfNull(other);
            double a = Luminance();
            double b = other.Luminance();
            return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
        }

        /// <summary>
        /// Distance between two colors in RGB space, 0 to about 441; below about 60 they look alike.
        /// </summary>
        /// <param name="other">Other color.</param>
        /// <returns>Distance.</returns>
        public double Distance(XenoRgb other)
        {
            ArgumentNullException.ThrowIfNull(other);
            double dr = R - other.R;
            double dg = G - other.G;
            double db = B - other.B;
            return Math.Sqrt(dr * dr + dg * dg + db * db);
        }

        #endregion

        #region Private-Methods

        private static double Linear(int channel)
        {
            double c = channel / 255.0;
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        #endregion
    }
}

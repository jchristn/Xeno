namespace Xeno.Email.Templates
{
    using System;

    /// <summary>
    /// Email template theme values.
    /// </summary>
    public class EmailTemplateTheme
    {
        /// <summary>
        /// Brand name.
        /// </summary>
        public string BrandName { get; set; } = "Xeno";

        /// <summary>
        /// Accent color.
        /// </summary>
        public string AccentColor { get; set; } = "#1f6feb";

        /// <summary>
        /// Text color.
        /// </summary>
        public string TextColor { get; set; } = "#1f2937";

        /// <summary>
        /// Muted text color.
        /// </summary>
        public string MutedTextColor { get; set; } = "#6b7280";

        /// <summary>
        /// Background color.
        /// </summary>
        public string BackgroundColor { get; set; } = "#f8fafc";

        /// <summary>
        /// Surface color.
        /// </summary>
        public string SurfaceColor { get; set; } = "#ffffff";

        /// <summary>
        /// Border color.
        /// </summary>
        public string BorderColor { get; set; } = "#d8dee9";
    }
}

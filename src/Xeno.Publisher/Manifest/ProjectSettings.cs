namespace Xeno.Publisher.Manifest
{
    /// <summary>
    /// Product identity, artwork, and text. Every installer that has a UI shows the display name, the logo, the tagline,
    /// and the license, so they are kept here once instead of in each format.
    /// </summary>
    public sealed class ProjectSettings
    {
        /// <summary>
        /// Lowercase name used in file names and package names, for example "xeno".
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Product name as users know it, for example "Xeno". Used in window titles, menus, and installed-app lists.
        /// </summary>
        public string DisplayName { get; set; } = "";

        /// <summary>
        /// Package identifier for winget and similar feeds, for example "JoelChristner.Xeno".
        /// </summary>
        public string PackageId { get; set; } = "";

        /// <summary>
        /// Reverse-DNS bundle identifier for the macOS app, for example "ai.xenocloud.xeno". The .pkg and the
        /// LaunchAgent derive their identifiers from it.
        /// </summary>
        public string BundleId { get; set; } = "";

        /// <summary>
        /// One sentence saying what the product is. Shown on every installer's welcome page.
        /// </summary>
        public string Tagline { get; set; } = "";

        /// <summary>
        /// A longer paragraph for package metadata (deb, rpm, winget, Homebrew).
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// SPDX license identifier, for example "MIT".
        /// </summary>
        public string License { get; set; } = "";

        /// <summary>
        /// License file relative to the repository root. Default LICENSE.md.
        /// </summary>
        public string LicenseFile { get; set; } = "LICENSE.md";

        /// <summary>
        /// Square PNG logo relative to the repository root. Every format's icon sizes are generated from it.
        /// </summary>
        public string Logo { get; set; } = "assets/logo.png";

        /// <summary>
        /// Windows icon (.ico) relative to the repository root.
        /// </summary>
        public string WindowsIcon { get; set; } = "assets/logo.ico";

        /// <summary>
        /// Publisher name shown in installed-app lists.
        /// </summary>
        public string Publisher { get; set; } = "";

        /// <summary>
        /// Maintainer field for deb and rpm metadata.
        /// </summary>
        public string Maintainer { get; set; } = "";

        /// <summary>
        /// Source repository URL.
        /// </summary>
        public string Repo { get; set; } = "";

        /// <summary>
        /// Project home page URL.
        /// </summary>
        public string Homepage { get; set; } = "";
    }
}

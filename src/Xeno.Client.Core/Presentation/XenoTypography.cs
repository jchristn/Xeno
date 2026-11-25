namespace Xeno.Client.Presentation
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;

    /// <summary>
    /// The fonts every Xeno front end uses: each operating system's own UI and code fonts, in one order shared by the
    /// web dashboards (as CSS stacks) and the desktop and tray apps (resolved to the first installed family). The
    /// bundled Inter is the last resort for Linux hosts without any of the listed families.
    /// </summary>
    public static class XenoTypography
    {
        #region Public-Members

        /// <summary>
        /// CSS font stack for text, used by the web dashboards (<c>--font-sans</c> in <c>dashboard/src/index.css</c>)
        /// and server-rendered pages.
        /// </summary>
        public const string CssSansStack = "-apple-system, BlinkMacSystemFont, \"Segoe UI Variable Text\", \"Segoe UI\", \"Noto Sans\", Ubuntu, Cantarell, Inter, \"Noto Sans CJK JP\", sans-serif";

        /// <summary>
        /// CSS font stack for code, identifiers, and logs (<c>--font-mono</c> in <c>dashboard/src/index.css</c>).
        /// </summary>
        public const string CssMonoStack = "ui-monospace, \"SF Mono\", SFMono-Regular, Menlo, \"Cascadia Mono\", Consolas, \"DejaVu Sans Mono\", \"Liberation Mono\", monospace";

        /// <summary>
        /// Heaviest font weight any front end uses. Text is 400, emphasis 500, headings and labels 600.
        /// </summary>
        public const int MaximumWeight = 600;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Text font families to try on an operating system, most preferred first.
        /// </summary>
        /// <param name="platform">Operating system.</param>
        /// <returns>Family names.</returns>
        public static IReadOnlyList<string> SansCandidates(OSPlatform platform)
        {
            if (platform == OSPlatform.OSX) return new List<string> { ".AppleSystemUIFont", "Helvetica Neue" };
            if (platform == OSPlatform.Windows) return new List<string> { "Segoe UI Variable Text", "Segoe UI" };
            return new List<string> { "Noto Sans", "Ubuntu", "Cantarell" };
        }

        /// <summary>
        /// Monospace font families to try on an operating system, most preferred first.
        /// </summary>
        /// <param name="platform">Operating system.</param>
        /// <returns>Family names.</returns>
        public static IReadOnlyList<string> MonoCandidates(OSPlatform platform)
        {
            if (platform == OSPlatform.OSX) return new List<string> { ".AppleSystemUIFontMonospaced", "Menlo" };
            if (platform == OSPlatform.Windows) return new List<string> { "Cascadia Mono", "Consolas" };
            return new List<string> { "DejaVu Sans Mono", "Liberation Mono", "Noto Sans Mono", "Ubuntu Mono" };
        }

        /// <summary>
        /// The operating system this process runs on, as used by <see cref="SansCandidates"/> and
        /// <see cref="MonoCandidates"/>. Anything other than macOS and Windows is treated as Linux.
        /// </summary>
        /// <returns>Operating system.</returns>
        public static OSPlatform CurrentPlatform()
        {
            if (OperatingSystem.IsMacOS()) return OSPlatform.OSX;
            if (OperatingSystem.IsWindows()) return OSPlatform.Windows;
            return OSPlatform.Linux;
        }

        /// <summary>
        /// The first candidate that is installed, or the fallback when none is.
        /// </summary>
        /// <param name="candidates">Family names, most preferred first.</param>
        /// <param name="isInstalled">Returns true when a family is installed. An exception counts as not installed.</param>
        /// <param name="fallback">Family to use when no candidate is installed.</param>
        /// <returns>Family name.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public static string Resolve(IEnumerable<string> candidates, Func<string, bool> isInstalled, string fallback)
        {
            ArgumentNullException.ThrowIfNull(candidates);
            ArgumentNullException.ThrowIfNull(isInstalled);
            ArgumentNullException.ThrowIfNull(fallback);
            foreach (string candidate in candidates)
            {
                if (String.IsNullOrWhiteSpace(candidate)) continue;
                bool installed;
                try
                {
                    installed = isInstalled(candidate);
                }
                catch (Exception)
                {
                    installed = false;
                }

                if (installed) return candidate;
            }

            return fallback;
        }

        #endregion
    }
}

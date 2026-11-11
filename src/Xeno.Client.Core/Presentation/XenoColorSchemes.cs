namespace Xeno.Client.Presentation
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    /// <summary>
    /// The built-in color schemes for the desktop app and the tray agent, the user's custom scheme, and the rules that
    /// turn a scheme's base colors into every color the apps draw with. Thread safe; the catalog is read only.
    /// </summary>
    public static class XenoColorSchemes
    {
        #region Public-Members

        /// <summary>
        /// Identifier of the original Xeno purple and green scheme, used when nothing else is chosen.
        /// </summary>
        public const string DefaultId = "xeno";

        /// <summary>
        /// Identifier of the user's own scheme, stored in <see cref="XenoFrontEndPreferences.DesktopCustomColors"/>.
        /// </summary>
        public const string CustomId = "custom";

        /// <summary>
        /// Minimum contrast for body text, sidebar text, and button text.
        /// </summary>
        public const double TextContrast = 4.5;

        /// <summary>
        /// Minimum contrast for the accent and status colors against panels.
        /// </summary>
        public const double GraphicContrast = 3.0;

        /// <summary>
        /// Built-in schemes, the default first.
        /// </summary>
        public static IReadOnlyList<XenoColorScheme> BuiltIn { get; } = BuildCatalog();

        #endregion

        #region Public-Methods

        /// <summary>
        /// Find a built-in scheme by identifier.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <returns>The scheme, or the default when the identifier is unknown.</returns>
        public static XenoColorScheme Find(string? id)
        {
            return BuiltIn.FirstOrDefault(item => String.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase)) ?? BuiltIn[0];
        }

        /// <summary>
        /// The scheme the preferences select: the custom scheme when chosen and valid, otherwise a built-in scheme.
        /// </summary>
        /// <param name="preferences">Preferences.</param>
        /// <returns>Scheme.</returns>
        /// <exception cref="ArgumentNullException">preferences is null.</exception>
        public static XenoColorScheme Selected(XenoFrontEndPreferences preferences)
        {
            ArgumentNullException.ThrowIfNull(preferences);
            if (String.Equals(preferences.DesktopColorScheme, CustomId, StringComparison.OrdinalIgnoreCase))
            {
                XenoColorScheme? custom = preferences.DesktopCustomColors;
                if (custom != null && IsValid(custom.Light) && IsValid(custom.Dark))
                {
                    custom.Id = CustomId;
                    custom.Name = "Custom";
                    return custom;
                }
            }

            return Find(preferences.DesktopColorScheme);
        }

        /// <summary>
        /// A custom scheme that starts from another scheme's colors.
        /// </summary>
        /// <param name="from">Scheme to copy.</param>
        /// <returns>Custom scheme holding the six base colors of each appearance.</returns>
        /// <exception cref="ArgumentNullException">from is null.</exception>
        public static XenoColorScheme CreateCustom(XenoColorScheme from)
        {
            ArgumentNullException.ThrowIfNull(from);
            return new XenoColorScheme
            {
                Id = CustomId,
                Name = "Custom",
                Description = "Your own colors.",
                Light = BaseOf(Resolve(from, false)),
                Dark = BaseOf(Resolve(from, true))
            };
        }

        /// <summary>
        /// True when the six base colors of a set are all valid colors.
        /// </summary>
        /// <param name="set">Set.</param>
        /// <returns>True when valid.</returns>
        public static bool IsValid(XenoColorSet? set)
        {
            if (set == null) return false;
            return new[] { set.Background, set.Panel, set.Text, set.Accent, set.Sidebar, set.SidebarActive }.All(value => XenoRgb.TryParse(value) != null);
        }

        /// <summary>
        /// Work out every color of one appearance of a scheme. Colors the set gives are used as they are; the rest are
        /// derived from the base colors and adjusted until they meet <see cref="TextContrast"/> or
        /// <see cref="GraphicContrast"/>.
        /// </summary>
        /// <param name="scheme">Scheme.</param>
        /// <param name="dark">True for the dark appearance.</param>
        /// <returns>Colors.</returns>
        /// <exception cref="ArgumentNullException">scheme is null.</exception>
        public static XenoResolvedColors Resolve(XenoColorScheme scheme, bool dark)
        {
            ArgumentNullException.ThrowIfNull(scheme);
            XenoColorSet set = (dark ? scheme.Dark : scheme.Light) ?? new XenoColorSet();
            XenoColorSet fallback = dark ? BuiltIn[0].Dark : BuiltIn[0].Light;
            XenoRgb background = Color(set.Background, fallback.Background);
            XenoRgb panel = Color(set.Panel, fallback.Panel);
            XenoRgb text = Color(set.Text, fallback.Text);
            XenoRgb accent = Color(set.Accent, fallback.Accent);
            XenoRgb sidebar = Color(set.Sidebar, fallback.Sidebar);
            XenoRgb active = Color(set.SidebarActive, fallback.SidebarActive);

            XenoRgb panelSoft = Given(set.PanelSoft) ?? panel.Mix(dark ? text : sidebar, dark ? 0.05 : 0.045);
            XenoRgb muted = Given(set.Muted) ?? Ensure(text.Mix(panel, 0.4), panelSoft, TextContrast + 0.1);
            XenoRgb border = Given(set.Border) ?? panel.Mix(text, dark ? 0.2 : 0.14);
            XenoRgb accentFill = Given(set.AccentFill) ?? accent;
            XenoRgb sidebarText = Given(set.SidebarText) ?? Ensure(Readable(sidebar).Mix(sidebar, 0.08), sidebar, 7.0);
            XenoRgb sidebarMuted = Given(set.SidebarMuted) ?? Ensure(sidebarText.Mix(sidebar, 0.4), sidebar, TextContrast);
            XenoRgb activeText = Given(set.SidebarActiveText) ?? Readable(active);

            XenoRgb success = Given(set.Success) ?? XenoRgb.Parse(dark ? "#4ade80" : "#1a7f37");
            XenoRgb warning = Given(set.Warning) ?? XenoRgb.Parse(dark ? "#fbbf24" : "#9a6700");
            XenoRgb danger = Given(set.Danger) ?? XenoRgb.Parse(dark ? "#f87171" : "#c62828");
            XenoRgb info = Given(set.Info) ?? accent;
            double softAmount = dark ? 0.2 : 0.11;
            XenoRgb successSoft = Given(set.SuccessSoft) ?? panel.Mix(success, softAmount);
            XenoRgb warningSoft = Given(set.WarningSoft) ?? panel.Mix(warning, softAmount);
            XenoRgb dangerSoft = Given(set.DangerSoft) ?? panel.Mix(danger, softAmount);
            XenoRgb infoSoft = Given(set.InfoSoft) ?? panel.Mix(info, softAmount);
            if (set.Success == null) success = Ensure(success, successSoft, TextContrast);
            if (set.Warning == null) warning = Ensure(warning, warningSoft, TextContrast);
            if (set.Danger == null) danger = Ensure(danger, dangerSoft, TextContrast);
            if (set.Info == null && set.InfoSoft == null) info = Ensure(info, infoSoft, TextContrast);

            List<string> series = set.Series != null && set.Series.Count > 0 && set.Series.All(item => XenoRgb.TryParse(item) != null)
                ? set.Series.Select(item => XenoRgb.Parse(item).ToHex()).ToList()
                : Series(accent, active, panel, dark);

            return new XenoResolvedColors
            {
                IsDark = dark,
                Background = background.ToHex(),
                Panel = panel.ToHex(),
                PanelSoft = panelSoft.ToHex(),
                Text = text.ToHex(),
                Muted = muted.ToHex(),
                Border = border.ToHex(),
                Accent = accent.ToHex(),
                AccentFill = accentFill.ToHex(),
                AccentText = set.ThemeAccentText ? null : (Given(set.AccentText) ?? Readable(accentFill)).ToHex(),
                Success = success.ToHex(),
                SuccessSoft = successSoft.ToHex(),
                Warning = warning.ToHex(),
                WarningSoft = warningSoft.ToHex(),
                Danger = danger.ToHex(),
                DangerSoft = dangerSoft.ToHex(),
                Info = info.ToHex(),
                InfoSoft = infoSoft.ToHex(),
                Sidebar = sidebar.ToHex(),
                SidebarText = sidebarText.ToHex(),
                SidebarMuted = sidebarMuted.ToHex(),
                SidebarActive = active.ToHex(),
                SidebarActiveText = activeText.ToHex(),
                Series = series
            };
        }

        /// <summary>
        /// Readability problems in a set of colors, for warning the user about a custom scheme. Empty when every pair
        /// meets <see cref="TextContrast"/> or <see cref="GraphicContrast"/>.
        /// </summary>
        /// <param name="set">Set.</param>
        /// <param name="dark">True for the dark appearance.</param>
        /// <returns>One sentence per problem.</returns>
        public static IReadOnlyList<string> Check(XenoColorSet set, bool dark)
        {
            ArgumentNullException.ThrowIfNull(set);
            List<string> problems = new List<string>();
            if (!IsValid(set))
            {
                problems.Add("Every color needs to be a hex value such as #4a2054.");
                return problems;
            }

            XenoResolvedColors colors = Resolve(new XenoColorScheme { Light = set, Dark = set }, dark);
            Require(problems, "Text on the background", colors.Text, colors.Background, TextContrast);
            Require(problems, "Text on panels", colors.Text, colors.Panel, TextContrast);
            Require(problems, "Secondary text on panels", colors.Muted, colors.Panel, TextContrast);
            Require(problems, "Sidebar text", colors.SidebarText, colors.Sidebar, TextContrast);
            Require(problems, "Selected sidebar text", colors.SidebarActiveText, colors.SidebarActive, TextContrast);
            Require(problems, "The accent on panels", colors.Accent, colors.Panel, GraphicContrast);
            if (colors.AccentText != null) Require(problems, "Button text", colors.AccentText, colors.AccentFill, TextContrast);
            return problems;
        }

        /// <summary>
        /// WCAG contrast ratio of two colors.
        /// </summary>
        /// <param name="first">First color.</param>
        /// <param name="second">Second color.</param>
        /// <returns>Ratio from 1 to 21.</returns>
        public static double Contrast(string first, string second)
        {
            return XenoRgb.Parse(first).Contrast(XenoRgb.Parse(second));
        }

        #endregion

        #region Private-Methods

        private static void Require(List<string> problems, string what, string foreground, string background, double minimum)
        {
            double ratio = Contrast(foreground, background);
            if (ratio < minimum) problems.Add(what + " is hard to read (" + ratio.ToString("0.0", CultureInfo.InvariantCulture) + ":1, aim for " + minimum.ToString("0.#", CultureInfo.InvariantCulture) + ":1).");
        }

        private static XenoColorSet BaseOf(XenoResolvedColors colors)
        {
            return new XenoColorSet { Background = colors.Background, Panel = colors.Panel, Text = colors.Text, Accent = colors.Accent, Sidebar = colors.Sidebar, SidebarActive = colors.SidebarActive };
        }

        private static XenoRgb Color(string? value, string fallback)
        {
            return XenoRgb.TryParse(value) ?? XenoRgb.Parse(fallback);
        }

        private static XenoRgb? Given(string? value)
        {
            return XenoRgb.TryParse(value);
        }

        private static XenoRgb Readable(XenoRgb background)
        {
            XenoRgb light = XenoRgb.Parse("#ffffff");
            XenoRgb ink = XenoRgb.Parse("#111318");
            return light.Contrast(background) >= ink.Contrast(background) ? light : ink;
        }

        private static XenoRgb Ensure(XenoRgb foreground, XenoRgb background, double minimum)
        {
            XenoRgb target = background.Luminance() > 0.18 ? XenoRgb.Black : XenoRgb.White;
            XenoRgb current = foreground;
            for (int step = 0; step < 20 && current.Contrast(background) < minimum; step++) current = current.Mix(target, 0.1);
            return current;
        }

        private static List<string> Series(XenoRgb accent, XenoRgb active, XenoRgb panel, bool dark)
        {
            string[] standard = dark
                ? new[] { "#fbbf24", "#38bdf8", "#f87171", "#4ade80", "#c084fc", "#94a3b8", "#fb923c", "#2dd4bf", "#f472b6", "#a3e635" }
                : new[] { "#b45309", "#0e7490", "#b91c1c", "#15803d", "#7c3aed", "#475569", "#c2410c", "#0f766e", "#be185d", "#4d7c0f" };
            List<XenoRgb> chosen = new List<XenoRgb> { Ensure(accent, panel, GraphicContrast) };
            XenoRgb second = Ensure(active, panel, GraphicContrast);
            if (second.Distance(chosen[0]) >= 80) chosen.Add(second);
            foreach (string hex in standard)
            {
                if (chosen.Count >= 6) break;
                XenoRgb candidate = XenoRgb.Parse(hex);
                if (chosen.All(item => item.Distance(candidate) >= 80)) chosen.Add(candidate);
            }

            return chosen.Select(item => item.ToHex()).ToList();
        }

        private static XenoColorScheme Scheme(string id, string name, string description, XenoColorSet light, XenoColorSet dark)
        {
            return new XenoColorScheme { Id = id, Name = name, Description = description, Light = light, Dark = dark };
        }

        private static XenoColorSet Set(string background, string panel, string text, string accent, string sidebar, string active, string? accentFill = null)
        {
            return new XenoColorSet { Background = background, Panel = panel, Text = text, Accent = accent, Sidebar = sidebar, SidebarActive = active, AccentFill = accentFill };
        }

        private static List<XenoColorScheme> BuildCatalog()
        {
            XenoColorSet xenoLight = new XenoColorSet
            {
                Background = "#f7f5f9", Panel = "#ffffff", PanelSoft = "#f4f0f6", Text = "#2a1530", Muted = "#6e5f75", Border = "#e3dbe8",
                Accent = XenoBrand.GreenDeep, AccentFill = XenoBrand.GreenDeep, ThemeAccentText = true,
                Success = XenoBrand.GreenDeep, SuccessSoft = "#e3f8eb", Warning = "#a15d00", WarningSoft = "#fff4df", Danger = "#c73737", DangerSoft = "#fdecec",
                Info = XenoBrand.Purple, InfoSoft = "#f1e8f4",
                Sidebar = XenoBrand.Purple, SidebarText = "#efe6f2", SidebarMuted = "#b9a6c1", SidebarActive = XenoBrand.Green, SidebarActiveText = XenoBrand.PurpleInk,
                Series = new List<string> { XenoBrand.GreenShade, XenoBrand.Purple, "#a15d00", "#0f6e8f", "#c73737", XenoBrand.Green }
            };
            XenoColorSet xenoDark = new XenoColorSet
            {
                Background = "#150b18", Panel = "#211327", PanelSoft = "#2b1933", Text = "#f3ecf5", Muted = "#b4a3bb", Border = "#3d2846",
                Accent = XenoBrand.Green, AccentFill = XenoBrand.GreenDeep, ThemeAccentText = true,
                Success = XenoBrand.Green, SuccessSoft = "#123a22", Warning = "#fbbf24", WarningSoft = "#412f15", Danger = "#f87171", DangerSoft = "#442121",
                Info = XenoBrand.Lavender, InfoSoft = "#352040",
                Sidebar = XenoBrand.PurpleDeep, SidebarText = "#e6dbea", SidebarMuted = "#a48fad", SidebarActive = XenoBrand.Green, SidebarActiveText = XenoBrand.PurpleInk,
                Series = new List<string> { XenoBrand.Green, XenoBrand.Lavender, "#fbbf24", "#38bdf8", "#f87171", XenoBrand.GreenShade }
            };

            return new List<XenoColorScheme>
            {
                Scheme(DefaultId, "Xeno", "The original purple and green.", xenoLight, xenoDark),
                Scheme("graphite", "Graphite", "Neutral grays with a clear blue accent.",
                    Set("#f5f6f8", "#ffffff", "#1f2328", "#0969da", "#24292f", "#0969da"),
                    Set("#0d1117", "#161b22", "#e6edf3", "#58a6ff", "#010409", "#1f6feb", "#1f6feb")),
                Scheme("ocean", "Ocean", "Deep navy with teal.",
                    Set("#f2f7fa", "#ffffff", "#0b2537", "#0e7490", "#0c2d48", "#22d3ee"),
                    Set("#07131d", "#0e1f2d", "#e3eef5", "#22d3ee", "#04101a", "#0891b2", "#0e7490")),
                Scheme("arctic", "Arctic", "Cool blue-grays and frost.",
                    Set("#eceff4", "#ffffff", "#2e3440", "#4c6a92", "#3b4252", "#88c0d0"),
                    Set("#2e3440", "#3b4252", "#eceff4", "#88c0d0", "#242933", "#88c0d0", "#5e81ac")),
                Scheme("forest", "Forest", "Evergreen with an amber highlight.",
                    Set("#f4f7f2", "#ffffff", "#1d2a1f", "#2f6b3a", "#1f3b2b", "#e0b84f"),
                    Set("#0f1a13", "#16251b", "#e6efe7", "#7fc28a", "#0a130d", "#e0b84f", "#2f6b3a")),
                Scheme("ember", "Ember", "Warm charcoal and orange.",
                    Set("#faf6f2", "#ffffff", "#2b1d14", "#c2410c", "#2b1d14", "#f97316"),
                    Set("#17110d", "#221913", "#f5ebe3", "#fb923c", "#0f0a07", "#f97316", "#c2410c")),
                Scheme("rose", "Rose", "Berry and soft pink.",
                    Set("#fbf5f7", "#ffffff", "#3a1d2b", "#be185d", "#4a1d33", "#f9a8d4"),
                    Set("#1a0f15", "#26161f", "#f8e8ef", "#f472b6", "#12080d", "#f472b6", "#be185d")),
                Scheme("indigo", "Indigo", "Ink blue with periwinkle.",
                    Set("#f5f6fb", "#ffffff", "#1e1b4b", "#4f46e5", "#1e1b4b", "#a5b4fc"),
                    Set("#0e0d1f", "#17162e", "#e8e7fb", "#a5b4fc", "#08071a", "#5249e0", "#4f46e5")),
                Scheme("sand", "Sand", "Warm paper with deep teal and gold.",
                    Set("#fdf6e3", "#fffbf0", "#3b3227", "#1f6fa8", "#073642", "#b58900"),
                    Set("#002b36", "#073642", "#eee8d5", "#5fb3e6", "#00212b", "#b58900", "#1f6fa8")),
                Scheme("lagoon", "Lagoon", "Teal water with a coral highlight.",
                    Set("#f2f8f7", "#ffffff", "#10302c", "#0f766e", "#134e4a", "#fb7185"),
                    Set("#071816", "#0d2522", "#e0f2ef", "#5eead4", "#041110", "#fb7185", "#0f766e")),
                Scheme("citrus", "Citrus", "Olive and lemon.",
                    Set("#fafaf2", "#ffffff", "#23260f", "#4d7c0f", "#1a2e05", "#facc15"),
                    Set("#11140a", "#1a1e10", "#eef2dc", "#a3e635", "#0b0e05", "#facc15", "#4d7c0f")),
                Scheme("paper", "Paper", "Light sidebar, quiet and bright.",
                    Set("#f7f7f5", "#ffffff", "#1f2328", "#2563eb", "#ecebe7", "#dbe4ff"),
                    Set("#1b1b1d", "#232326", "#ececec", "#8ab4f8", "#2a2a2e", "#3b4a6b", "#2563eb")),
                Scheme("mono", "High Contrast", "Black and white for the clearest text.",
                    Set("#f2f2f2", "#ffffff", "#000000", "#000000", "#000000", "#ffffff"),
                    Set("#000000", "#121212", "#ffffff", "#ffffff", "#000000", "#ffffff", "#e5e5e5"))
            };
        }

        #endregion
    }
}

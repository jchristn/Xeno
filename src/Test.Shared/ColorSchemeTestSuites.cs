namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using Xeno.Client.Presentation;

    /// <summary>
    /// Desktop color schemes: every built-in scheme is readable in light and dark, the original scheme keeps its colors,
    /// custom schemes are derived and checked, and the choice round-trips through frontend.json.
    /// </summary>
    public static class ColorSchemeTestSuites
    {
        /// <summary>
        /// Color scheme suite.
        /// </summary>
        /// <returns>Suite.</returns>
        public static TestSuiteDescriptor ColorSchemeSuite()
        {
            const string suite = "ColorSchemes";
            return new TestSuiteDescriptor(suite, "Desktop color schemes", new List<TestCaseDescriptor>
            {
                Case(suite, "CatalogHasDistinctSchemes", "There are at least twelve schemes besides the default, with unique identifiers and names", () =>
                {
                    IReadOnlyList<XenoColorScheme> all = XenoColorSchemes.BuiltIn;
                    Check(all[0].Id == XenoColorSchemes.DefaultId && all.Count >= 13, "Expected the default first and at least 13 schemes, got " + all.Count + ".");
                    Check(all.Select(item => item.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() == all.Count && all.Select(item => item.Name).Distinct().Count() == all.Count, "Scheme identifiers or names repeat.");
                    Check(all.All(item => item.Id != XenoColorSchemes.CustomId), "No built-in scheme may use the custom identifier.");
                    Check(XenoColorSchemes.Find("nope").Id == XenoColorSchemes.DefaultId && XenoColorSchemes.Find("OCEAN").Id == "ocean", "Find should fall back to the default and ignore case.");
                }),
                Case(suite, "EveryBuiltInSchemeIsReadable", "Every built-in scheme meets the contrast minimums in light and dark", () =>
                {
                    List<string> failures = new List<string>();
                    foreach (XenoColorScheme scheme in XenoColorSchemes.BuiltIn)
                    {
                        foreach (bool dark in new[] { false, true })
                        {
                            XenoResolvedColors c = XenoColorSchemes.Resolve(scheme, dark);
                            string where = scheme.Id + (dark ? " dark" : " light");
                            Need(failures, where, "text/background", c.Text, c.Background, 7.0);
                            Need(failures, where, "text/panel", c.Text, c.Panel, 7.0);
                            Need(failures, where, "muted/panel", c.Muted, c.Panel, 4.5);
                            Need(failures, where, "muted/panelSoft", c.Muted, c.PanelSoft, 4.5);
                            Need(failures, where, "sidebarText/sidebar", c.SidebarText, c.Sidebar, 4.5);
                            Need(failures, where, "sidebarMuted/sidebar", c.SidebarMuted, c.Sidebar, scheme.Id == XenoColorSchemes.DefaultId ? 3.0 : 4.5);
                            Need(failures, where, "activeText/active", c.SidebarActiveText, c.SidebarActive, 4.5);
                            Need(failures, where, "accent/panel", c.Accent, c.Panel, 3.0);
                            if (c.AccentText != null) Need(failures, where, "accentText/accentFill", c.AccentText, c.AccentFill, 4.5);
                            Need(failures, where, "success/soft", c.Success, c.SuccessSoft, 3.0);
                            Need(failures, where, "warning/soft", c.Warning, c.WarningSoft, 3.0);
                            Need(failures, where, "danger/soft", c.Danger, c.DangerSoft, 3.0);
                            Need(failures, where, "info/soft", c.Info, c.InfoSoft, 3.0);
                            if (XenoRgb.Parse(c.Info).Distance(XenoRgb.Parse(c.Success)) < 60) failures.Add(where + " info " + c.Info + " looks like success " + c.Success + " (the two chart series would match)");
                            if (c.Series.Count < 5) failures.Add(where + " has only " + c.Series.Count + " chart colors.");
                            if (scheme.Id != XenoColorSchemes.DefaultId)
                            {
                                XenoColorScheme copy = XenoColorSchemes.CreateCustom(scheme);
                                foreach (string problem in XenoColorSchemes.Check(dark ? copy.Dark : copy.Light, dark)) failures.Add(where + " as custom: " + problem);
                            }
                        }
                    }

                    Check(failures.Count == 0, "Contrast failures:" + Environment.NewLine + String.Join(Environment.NewLine, failures));
                }),
                Case(suite, "DefaultSchemeKeepsTheOriginalColors", "The Xeno scheme resolves to the colors the desktop app has always used", () =>
                {
                    XenoResolvedColors light = XenoColorSchemes.Resolve(XenoColorSchemes.Find(XenoColorSchemes.DefaultId), false);
                    XenoResolvedColors dark = XenoColorSchemes.Resolve(XenoColorSchemes.Find(XenoColorSchemes.DefaultId), true);
                    Check(light.Background == "#f7f5f9" && light.Sidebar == "#55295f" && light.SidebarActive == "#4bbf80" && light.AccentFill == XenoBrand.GreenDeep && light.AccentText == null, "Light default changed.");
                    Check(dark.Background == "#150b18" && dark.Accent == XenoBrand.Green && dark.AccentFill == XenoBrand.GreenDeep && dark.Muted == "#b4a3bb", "Dark default changed.");
                }),
                Case(suite, "CustomSchemesAreDerivedAndChecked", "A custom scheme needs six colors, derives the rest, and reports unreadable pairs", () =>
                {
                    XenoColorSet set = new XenoColorSet { Background = "#ffffff", Panel = "#ffffff", Text = "#eeeeee", Accent = "#ffff00", Sidebar = "#ffffff", SidebarActive = "#ffff00" };
                    IReadOnlyList<string> problems = XenoColorSchemes.Check(set, false);
                    Check(problems.Any(item => item.StartsWith("Text on the background", StringComparison.Ordinal)) && problems.Any(item => item.StartsWith("The accent", StringComparison.Ordinal)), "Unreadable colors were not reported: " + String.Join(" | ", problems));
                    XenoResolvedColors resolved = XenoColorSchemes.Resolve(new XenoColorScheme { Light = set, Dark = set }, false);
                    Check(XenoColorSchemes.Contrast(resolved.SidebarText, resolved.Sidebar) >= 4.5 && XenoColorSchemes.Contrast(resolved.SidebarActiveText, resolved.SidebarActive) >= 4.5, "Derived sidebar text should still be readable.");
                    set.Accent = "not a color";
                    Check(!XenoColorSchemes.IsValid(set) && XenoColorSchemes.Check(set, false).Count == 1, "An invalid color should be the only problem reported.");
                    Check(XenoRgb.TryParse("#abc")?.ToHex() == "#aabbcc" && XenoRgb.TryParse(" 4A2054 ")?.ToHex() == "#4a2054" && XenoRgb.TryParse("#12345") == null, "Hex parsing is wrong.");
                }),
                Case(suite, "TerminalAppUsesTheChosenScheme", "The terminal app keeps its original theme for Xeno and follows another palette when one is chosen", () =>
                {
                    Check(Object.ReferenceEquals(Xeno.Tui.XenoTuiTheme.For(XenoColorSchemes.Find(XenoColorSchemes.DefaultId), false), Xeno.Tui.XenoTuiTheme.Dark), "The Xeno palette should keep the original dark theme.");
                    Check(Object.ReferenceEquals(Xeno.Tui.XenoTuiTheme.For(XenoColorSchemes.Find(XenoColorSchemes.DefaultId), true), Xeno.Tui.XenoTuiTheme.Light), "The Xeno palette should keep the original light theme.");
                    foreach (XenoColorScheme scheme in XenoColorSchemes.BuiltIn)
                    {
                        foreach (bool light in new[] { false, true })
                        {
                            TUIKit.Theming.Theme theme = Xeno.Tui.XenoTuiTheme.For(scheme, light);
                            if (scheme.Id == XenoColorSchemes.DefaultId) continue;
                            XenoResolvedColors colors = XenoColorSchemes.Resolve(scheme, !light);
                            Check(theme.Accent.Foreground.Equals(Xeno.Tui.XenoTuiTheme.ToColor(colors.Accent)), scheme.Id + (light ? " light" : " dark") + " did not use the palette's accent.");
                        }
                    }

                    TUIKit.Widgets.HeatMapPalette ocean = Xeno.Tui.XenoTuiTheme.HeatPalette(Xeno.Tui.XenoTuiTheme.For(XenoColorSchemes.Find("ocean"), false));
                    TUIKit.Widgets.HeatMapPalette ember = Xeno.Tui.XenoTuiTheme.HeatPalette(Xeno.Tui.XenoTuiTheme.For(XenoColorSchemes.Find("ember"), false));
                    Check(!ocean.ColorForLevel(4, 5).Equals(ember.ColorForLevel(4, 5)), "The heat map colors should follow the palette.");
                    Check(ocean.EmptyColor.Equals(Xeno.Tui.XenoTuiTheme.ToColor(XenoColorSchemes.Resolve(XenoColorSchemes.Find("ocean"), true).Muted)), "Empty heat map cells should use the palette's muted color.");

                    using (TuiHarness tui = new TuiHarness(120, 36, XenoPageEnum.Settings))
                    {
                        tui.Tui.Context.Preferences.TerminalColorScheme = "ocean";
                        tui.Tui.Context.Restyle();
                        tui.Settle();
                        Check(tui.App.Theme.Name.StartsWith("Ocean", StringComparison.Ordinal), "The running app did not switch to Ocean: " + tui.App.Theme.Name);
                        Check(tui.Screen().Contains("Colors", StringComparison.Ordinal), "The Settings page has no Colors choice:" + Environment.NewLine + tui.Screen());
                    }
                }),
                Case(suite, "SchemeChoiceRoundTrips", "The chosen and custom schemes save to frontend.json and load back, and a broken custom scheme falls back", () =>
                {
                    string file = Path.Combine(Path.GetTempPath(), "xeno-colors-" + Guid.NewGuid().ToString("N") + ".json");
                    try
                    {
                        XenoFrontEndPreferences preferences = new XenoFrontEndPreferences { DesktopColorScheme = XenoColorSchemes.CustomId, DesktopCustomColors = XenoColorSchemes.CreateCustom(XenoColorSchemes.Find("forest")) };
                        preferences.DesktopCustomColors.Light.Accent = "#123456";
                        Check(preferences.Save(file), "Preferences did not save.");
                        XenoFrontEndPreferences loaded = XenoFrontEndPreferences.Load(file);
                        XenoColorScheme selected = XenoColorSchemes.Selected(loaded);
                        Check(selected.Id == XenoColorSchemes.CustomId && selected.Light.Accent == "#123456", "The custom scheme did not round-trip.");
                        loaded.DesktopCustomColors!.Dark.Sidebar = "oops";
                        Check(XenoColorSchemes.Selected(loaded).Id == XenoColorSchemes.DefaultId, "A broken custom scheme should fall back to the default.");
                        Check(XenoColorSchemes.Selected(new XenoFrontEndPreferences { DesktopColorScheme = "ember" }).Id == "ember", "A built-in choice was not selected.");
                    }
                    finally
                    {
                        File.Delete(file);
                    }
                })
            });
        }

        private static void Need(List<string> failures, string where, string pair, string foreground, string background, double minimum)
        {
            double ratio = XenoColorSchemes.Contrast(foreground, background);
            if (ratio < minimum) failures.Add(where + " " + pair + " " + foreground + " on " + background + " = " + ratio.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " (need " + minimum + ")");
        }

        private static TestCaseDescriptor Case(string suite, string id, string name, Action body)
        {
            return new TestCaseDescriptor(suite, id, name, executeAsync: delegate
            {
                body();
                return Task.CompletedTask;
            });
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}

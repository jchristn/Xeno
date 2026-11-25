namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using Xeno.Client.Presentation;

    /// <summary>
    /// Fonts: every front end uses the operating system's own UI and code fonts from <see cref="XenoTypography"/>, the
    /// web dashboards declare the same stacks, and no surface goes heavier than weight 600.
    /// </summary>
    public static class TypographyTestSuites
    {
        /// <summary>
        /// Typography suite.
        /// </summary>
        /// <returns>Suite.</returns>
        public static TestSuiteDescriptor TypographySuite()
        {
            const string suite = "Typography";
            return new TestSuiteDescriptor(suite, "Fonts across front ends", new List<TestCaseDescriptor>
            {
                Case(suite, "ResolvePicksFirstInstalled", "Resolve returns the first installed family, skips failures, and falls back", () =>
                {
                    List<string> candidates = new List<string> { "Missing", "Broken", "Second", "Third" };
                    Func<string, bool> installed = name =>
                    {
                        if (name == "Broken") throw new InvalidOperationException("probe failed");
                        return name == "Second" || name == "Third";
                    };
                    Check(XenoTypography.Resolve(candidates, installed, "Fallback") == "Second", "Expected the first installed family.");
                    Check(XenoTypography.Resolve(candidates, name => false, "Fallback") == "Fallback", "Expected the fallback when nothing is installed.");
                    Check(XenoTypography.Resolve(new List<string> { "", " " }, name => true, "Fallback") == "Fallback", "Blank candidates must be skipped.");
                }),
                Case(suite, "EveryPlatformPrefersItsSystemFonts", "macOS, Windows, and Linux each list their own UI and code fonts first", () =>
                {
                    Check(XenoTypography.SansCandidates(OSPlatform.OSX)[0] == ".AppleSystemUIFont", "macOS should prefer the system UI font.");
                    Check(XenoTypography.MonoCandidates(OSPlatform.OSX)[0] == ".AppleSystemUIFontMonospaced", "macOS should prefer SF Mono.");
                    Check(XenoTypography.SansCandidates(OSPlatform.Windows).Contains("Segoe UI"), "Windows should list Segoe UI.");
                    Check(XenoTypography.MonoCandidates(OSPlatform.Windows).Contains("Consolas"), "Windows should list Consolas.");
                    Check(XenoTypography.SansCandidates(OSPlatform.Linux)[0] == "Noto Sans", "Linux should prefer Noto Sans.");
                    Check(XenoTypography.MonoCandidates(OSPlatform.Linux)[0] == "DejaVu Sans Mono", "Linux should prefer DejaVu Sans Mono.");
                    foreach (OSPlatform platform in new[] { OSPlatform.OSX, OSPlatform.Windows, OSPlatform.Linux })
                    {
                        Check(XenoTypography.SansCandidates(platform).All(name => !name.Contains("Inter", StringComparison.Ordinal)), "Inter is the fallback, never a candidate.");
                    }
                }),
                Case(suite, "WebDashboardUsesTheSharedStacks", "dashboard/src/index.css declares XenoTypography's stacks and uses them for every font", () =>
                {
                    string css = File.ReadAllText(Path.Combine(RepositoryRoot(), "dashboard", "src", "index.css"));
                    Check(css.Contains("--font-sans: " + XenoTypography.CssSansStack + ";", StringComparison.Ordinal), "--font-sans differs from XenoTypography.CssSansStack.");
                    Check(css.Contains("--font-mono: " + XenoTypography.CssMonoStack + ";", StringComparison.Ordinal), "--font-mono differs from XenoTypography.CssMonoStack.");
                    List<string> stray = Regex.Matches(css, @"font-family:\s*([^;]+);").Select(match => match.Groups[1].Value.Trim())
                        .Where(value => value != "var(--font-sans)" && value != "var(--font-mono)" && value != "inherit").ToList();
                    Check(stray.Count == 0, "font-family must use var(--font-sans), var(--font-mono), or inherit: " + String.Join(" | ", stray));
                }),
                Case(suite, "NoSurfaceIsHeavierThanSemibold", "The web dashboard never sets a font weight above 600", () =>
                {
                    string css = File.ReadAllText(Path.Combine(RepositoryRoot(), "dashboard", "src", "index.css"));
                    List<string> heavy = Regex.Matches(css, @"font-weight:\s*(\d+|bold|bolder)").Select(match => match.Groups[1].Value)
                        .Where(value => !Int32.TryParse(value, out int weight) || weight > XenoTypography.MaximumWeight).ToList();
                    Check(heavy.Count == 0, "Weights above " + XenoTypography.MaximumWeight + ": " + String.Join(", ", heavy));
                }),
                Case(suite, "DesktopUsesTheResolvedFonts", "The desktop and tray apps take their fonts from XenoFonts, never a hard-coded family", () =>
                {
                    string root = RepositoryRoot();
                    foreach (string program in new[] { Path.Combine("src", "Xeno.Desktop", "Program.cs"), Path.Combine("src", "Xeno.Tray", "Program.cs") })
                    {
                        Check(File.ReadAllText(Path.Combine(root, program)).Contains("DefaultFamilyName = XenoFonts.SansFamilyName", StringComparison.Ordinal), program + " must default to XenoFonts.SansFamilyName.");
                    }

                    List<string> literal = new List<string>();
                    foreach (string folder in new[] { "Xeno.Desktop", "Xeno.Desktop.Common", "Xeno.Tray" })
                    {
                        foreach (string file in Directory.EnumerateFiles(Path.Combine(root, "src", folder), "*.cs", SearchOption.AllDirectories))
                        {
                            if (file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal) || file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal)) continue;
                            if (Regex.IsMatch(File.ReadAllText(file), "new FontFamily\\(\"")) literal.Add(Path.GetFileName(file));
                        }
                    }

                    Check(literal.Count == 0, "Hard-coded font families in: " + String.Join(", ", literal));
                })
            });
        }

        private static string RepositoryRoot()
        {
            DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "dashboard")) && Directory.Exists(Path.Combine(directory.FullName, "src"))) return directory.FullName;
                directory = directory.Parent;
            }

            throw new InvalidOperationException("Could not find the repository root from " + AppContext.BaseDirectory + ".");
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

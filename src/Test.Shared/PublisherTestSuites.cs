namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using SkiaSharp;
    using Touchstone.Core;
    using Xeno.Publisher;
    using Xeno.Publisher.Channels;
    using Xeno.Publisher.Manifest;

    /// <summary>
    /// The installer pipeline (src/Xeno.Publisher): the committed publisher.json, the single version source, asset
    /// names, and the files each installer format is built from.
    /// </summary>
    public static class PublisherTestSuites
    {
        /// <summary>
        /// Publisher suite.
        /// </summary>
        /// <returns>Suite.</returns>
        public static TestSuiteDescriptor PublisherSuite()
        {
            const string suite = "Publisher";
            return new TestSuiteDescriptor(suite, "Installer pipeline", new List<TestCaseDescriptor>
            {
                new TestCaseDescriptor(suite, "ManifestIsValid", "publisher.json loads, validates, covers six runtimes, and declares every installer channel", executeAsync: delegate
                {
                    PublisherManifest manifest = Manifest();
                    Check(manifest.Validate().Count == 0, "publisher.json has problems: " + String.Join(" ", manifest.Validate()));
                    Check(manifest.Build.Runtimes.OrderBy(item => item, StringComparer.Ordinal).SequenceEqual(new[] { "linux-arm64", "linux-x64", "osx-arm64", "osx-x64", "win-arm64", "win-x64" }), "The manifest must build all six runtimes.");
                    foreach (string id in new[] { "inno", "macos", "debrpm", "appimage", "archive" }) Check(manifest.Channels.TryGetValue(id, out ChannelSettings? channel) && channel.Enabled, "Installer channel " + id + " must be declared and enabled.");
                    foreach (string id in manifest.Channels.Keys) Check(ChannelRegistry.Resolve(id) != null, "publisher.json declares unknown channel " + id + ".");
                    Check(!manifest.Signing.MacOs.IsConfigured && !manifest.Signing.Windows.IsConfigured && !manifest.Signing.Linux.IsConfigured, "Xeno is not code-signed by policy; every signing target must stay unconfigured.");
                    ArtifactSettings client = Client(manifest);
                    Check(client.PathCommands.SequenceEqual(new[] { "xeno" }) && client.LoginItem != null && client.LoginItem.Executable == "xeno-tray" && client.MainExecutable == "xeno-desktop", "The client artifact must put xeno on PATH, start xeno-tray at login, and open xeno-desktop.");
                    Check(!String.IsNullOrEmpty(manifest.Channels["inno"].Options["appId"]), "The Inno Setup AppId must be fixed so upgrades replace in place.");
                    Check(File.Exists(Path.Combine(RepositoryRoot(), manifest.Project.Logo)) && File.Exists(Path.Combine(RepositoryRoot(), manifest.Project.LicenseFile)) && File.Exists(Path.Combine(RepositoryRoot(), manifest.Project.WindowsIcon)), "The logo, icon, and license the installers show must exist.");
                    return Task.CompletedTask;
                }),
                new TestCaseDescriptor(suite, "ManifestValidationCatchesMistakes", "Validation names a PATH command, login item, or shortcut that is not one of the artifact's executables", executeAsync: delegate
                {
                    string json = "{ \"project\": { \"name\": \"x\", \"displayName\": \"X\", \"tagline\": \"T\", \"bundleId\": \"a.b\" }, \"build\": { \"framework\": \"net10.0\", \"runtimes\": [\"win-x64\", \"amiga-68k\"], \"artifacts\": [ { \"id\": \"a\", \"csproj\": \"a.csproj\", \"mainExecutable\": \"a\", \"executables\": [\"a\"], \"pathCommands\": [\"b\"], \"loginItem\": { \"executable\": \"c\" }, \"shortcuts\": [ { \"displayName\": \"D\", \"executable\": \"d\" } ] } ] }, \"channels\": { \"inno\": { \"enabled\": true, \"artifact\": \"missing\" } } }";
                    try
                    {
                        PublisherManifest.Parse(json, "test.json");
                        throw new InvalidOperationException("An invalid manifest was accepted.");
                    }
                    catch (InvalidDataException ex)
                    {
                        foreach (string expected in new[] { "amiga-68k", "'b' on PATH", "'c' at login", "Shortcut 'D'", "unknown artifact 'missing'" }) Check(ex.Message.Contains(expected, StringComparison.Ordinal), "Validation did not mention " + expected + ": " + ex.Message);
                    }

                    return Task.CompletedTask;
                }),
                new TestCaseDescriptor(suite, "VersionHasOneSource", "src/Directory.Build.props holds the version, Constants.Version matches it, and CHANGELOG.md has its section", executeAsync: delegate
                {
                    string version = VersionReader.Read(RepositoryRoot());
                    Check(version == Xeno.Core.Constants.Version, "Constants.Version (" + Xeno.Core.Constants.Version + ") differs from src/Directory.Build.props (" + version + ").");
                    foreach (string csproj in Directory.GetFiles(Path.Combine(RepositoryRoot(), "src"), "*.csproj", SearchOption.AllDirectories).Where(path => !path.Contains(Path.DirectorySeparatorChar + "sdk" + Path.DirectorySeparatorChar, StringComparison.Ordinal) && !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal)))
                    {
                        Check(!File.ReadAllText(csproj).Contains("<Version>", StringComparison.Ordinal), Path.GetFileName(csproj) + " sets its own <Version>; only src/Directory.Build.props may.");
                    }

                    PublisherManifest manifest = Manifest();
                    string section = ChangelogReader.ReadSection(Path.Combine(RepositoryRoot(), manifest.ReleaseNotes.Source), manifest.ReleaseNotes.Heading, version);
                    Check(section.Length > 0, "CHANGELOG.md has no non-empty '## [" + version + "]' section; the release job refuses to publish without one.");
                    return Task.CompletedTask;
                }),
                new TestCaseDescriptor(suite, "AssetNames", "Every asset keeps the version in its name, and Debian and RPM follow their own conventions", executeAsync: delegate
                {
                    Check(AssetNames.WindowsSetup("xeno", "0.2.0", RuntimeTarget.Parse("win-arm64")) == "xeno-0.2.0-win-arm64-setup.exe", "Windows setup name is wrong.");
                    Check(AssetNames.MacPackage("xeno", "0.2.0", RuntimeTarget.Parse("osx-arm64")) == "xeno-0.2.0-osx-arm64.pkg" && AssetNames.MacDiskImage("xeno", "0.2.0", RuntimeTarget.Parse("osx-x64")) == "xeno-0.2.0-osx-x64.dmg", "macOS names are wrong.");
                    Check(AssetNames.Deb("xeno", "0.2.0", RuntimeTarget.Parse("linux-x64")) == "xeno_0.2.0_amd64.deb" && AssetNames.Deb("xeno", "0.2.0", RuntimeTarget.Parse("linux-arm64")) == "xeno_0.2.0_arm64.deb", "Debian names are wrong.");
                    Check(AssetNames.Rpm("xeno", "0.2.0", RuntimeTarget.Parse("linux-x64")) == "xeno-0.2.0-1.x86_64.rpm" && AssetNames.Rpm("xeno", "0.2.0", RuntimeTarget.Parse("linux-arm64")) == "xeno-0.2.0-1.aarch64.rpm", "RPM names are wrong.");
                    Check(AssetNames.AppImage("xeno", "0.2.0", RuntimeTarget.Parse("linux-arm64")) == "xeno-0.2.0-aarch64.AppImage", "AppImage name is wrong.");
                    Check(AssetNames.Archive("xeno", "0.2.0", RuntimeTarget.Parse("win-x64")) == "xeno-0.2.0-win-x64.zip" && AssetNames.Archive("xeno", "0.2.0", RuntimeTarget.Parse("linux-x64")) == "xeno-0.2.0-linux-x64.tar.gz", "Archive names are wrong.");
                    Check(!RuntimeTarget.TryParse("linux-musl-x64", out RuntimeTarget? _) && !RuntimeTarget.TryParse("win-x86", out RuntimeTarget? _), "Unsupported runtimes must be rejected.");
                    return Task.CompletedTask;
                }),
                new TestCaseDescriptor(suite, "ChangelogSection", "Release notes come from exactly one version's section", executeAsync: delegate
                {
                    string text = "# Changelog\r\n\r\n## [Unreleased]\r\n\r\n## [0.2.0] - 2026-11-19\r\n\r\n- New thing.\r\n- Other thing.\r\n\r\n## [0.1.1] - 2026-10-03\r\n\r\n- Old thing.\r\n";
                    Check(ChangelogReader.Section(text, "## [{version}]", "0.2.0") == "- New thing.\n- Other thing.", "The 0.2.0 section was not extracted.");
                    Check(ChangelogReader.Section(text, "## [{version}]", "0.1.1") == "- Old thing.", "The last section was not extracted.");
                    Check(ChangelogReader.Section(text, "## [{version}]", "0.3.0") == "" && ChangelogReader.Section(text, "## [{version}]", "Unreleased") == "", "A missing or empty section must read as empty.");
                    Check(ChangelogReader.Section(text, "## [{version}]", "0.2") == "", "A version prefix must not match a longer version.");
                    return Task.CompletedTask;
                }),
                new TestCaseDescriptor(suite, "InnoScript", "The Windows installer shows the logo, tagline, and license, adds PATH, registers the login item, and stops the client first", executeAsync: delegate
                {
                    PublisherManifest manifest = Manifest();
                    InnoScriptInput input = new InnoScriptInput
                    {
                        AppId = manifest.Channels["inno"].Options["appId"],
                        Project = manifest.Project,
                        Artifact = Client(manifest),
                        Version = "0.2.0",
                        Target = RuntimeTarget.Parse("win-arm64"),
                        PayloadDir = "C:\\stage\\payload",
                        OutputDir = "C:\\out",
                        OutputBaseName = "xeno-0.2.0-win-arm64-setup",
                        WizardImages = new List<string> { "wizard-100.bmp", "wizard-200.bmp" },
                        WizardSmallImages = new List<string> { "wizard-small-100.bmp" }
                    };
                    string script = InnoScript.Render(input);
                    foreach (string expected in new[]
                    {
                        "AppId={{" + input.AppId + "}", "AppName=Xeno\n", "LicenseFile=LICENSE.txt", "SetupIconFile=setup.ico", "WizardImageFile=wizard-100.bmp,wizard-200.bmp",
                        "WizardSmallImageFile=wizard-small-100.bmp", "ArchitecturesAllowed=arm64", "ChangesEnvironment=yes", "UninstallDisplayIcon={app}\\xeno-desktop.exe",
                        "WelcomeLabel2=" + manifest.Project.Tagline, "The xeno command, added to PATH", "ValueData: \"{olddata};{app}\"; Check: NeedsAddPath",
                        "ValueName: \"Xeno Agent\"", "Tasks: loginitem", "Name: \"{group}\\Xeno Terminal\"; Filename: \"{app}\\xeno.exe\"; Parameters: \"tui\"",
                        "runasoriginaluser", "Exec(ExpandConstant('{app}\\xeno.exe'), 'stop'", "/F /IM xeno-tray.exe", "RemovePath(ExpandConstant('{app}'))", "OutputBaseFilename=xeno-0.2.0-win-arm64-setup"
                    })
                    {
                        Check(script.Contains(expected, StringComparison.Ordinal), "The Inno script is missing: " + expected);
                    }

                    Check(InnoScript.Render(new InnoScriptInput { AppId = "A", Project = manifest.Project, Artifact = Client(manifest), Target = RuntimeTarget.Parse("win-x64") }).Contains("ArchitecturesAllowed=x64compatible", StringComparison.Ordinal), "x64 builds must allow x64compatible.");
                    Check(InnoScript.NumericVersion("0.2.0") == "0.2.0.0" && InnoScript.NumericVersion("1.4.0-alpha.2") == "1.4.0.0", "Numeric versions are wrong.");
                    Check(InnoChannel.WinePath("/Users/me/installers/0.2.0/.staging") == "Z:\\Users\\me\\installers\\0.2.0\\.staging", "Wine paths are wrong.");
                    return Task.CompletedTask;
                }),
                new TestCaseDescriptor(suite, "MacPackageFiles", "The .pkg shows a welcome page, license, and background, links xeno into /usr/local/bin, and installs the agent", executeAsync: delegate
                {
                    PublisherManifest manifest = Manifest();
                    ArtifactSettings client = Client(manifest);
                    string distribution = MacBundleFiles.DistributionXml(manifest.Project, "0.2.0", RuntimeTarget.Parse("osx-x64"), "component.pkg");
                    foreach (string expected in new[] { "<title>Xeno</title>", "<welcome file=\"welcome.html\"", "<license file=\"LICENSE.txt\"", "<background file=\"background.png\"", "<background-darkAqua", "alignment=\"bottomleft\" scaling=\"none\"", "hostArchitectures=\"x86_64,arm64\"", "ai.xenocloud.xeno.pkg" })
                    {
                        Check(distribution.Contains(expected, StringComparison.Ordinal), "The distribution file is missing: " + expected);
                    }

                    string postinstall = MacBundleFiles.PostinstallScript(manifest.Project, client);
                    Check(postinstall.Contains("/bin/mkdir -p /usr/local/bin", StringComparison.Ordinal) && postinstall.Contains("ln -sf \"/Applications/Xeno.app/Contents/MacOS/xeno\" /usr/local/bin/xeno", StringComparison.Ordinal), "postinstall must link xeno into a /usr/local/bin it creates.");
                    Check(postinstall.Contains("launchctl bootstrap \"gui/$uid\" \"/Library/LaunchAgents/ai.xenocloud.xeno.agent.plist\"", StringComparison.Ordinal) && postinstall.IndexOf("stat -f%Su /dev/console", StringComparison.Ordinal) < postinstall.IndexOf("bootstrap", StringComparison.Ordinal), "postinstall must start the agent for the console user, not root.");
                    string preinstall = MacBundleFiles.PreinstallScript(manifest.Project, client);
                    Check(preinstall.Contains("sudo -H -u \"$console_user\" \"/Applications/Xeno.app/Contents/MacOS/xeno\" stop", StringComparison.Ordinal) && preinstall.Contains("pkill -x xeno-tray", StringComparison.Ordinal), "preinstall must stop the client and the apps.");
                    string agent = MacBundleFiles.LaunchAgentPlist(manifest.Project, client);
                    Check(agent.Contains("<string>ai.xenocloud.xeno.agent</string>", StringComparison.Ordinal) && agent.Contains("/Applications/Xeno.app/Contents/MacOS/xeno-tray", StringComparison.Ordinal) && agent.Contains("<key>RunAtLoad</key>", StringComparison.Ordinal) && !agent.Contains("KeepAlive", StringComparison.Ordinal), "The LaunchAgent must start xeno-tray at login without keeping it alive.");
                    string info = MacBundleFiles.InfoPlist(manifest.Project, client, "0.2.0");
                    Check(info.Contains("<key>CFBundleExecutable</key>\n  <string>xeno-desktop</string>", StringComparison.Ordinal) && info.Contains("<string>ai.xenocloud.xeno</string>", StringComparison.Ordinal) && !info.Contains("LSUIElement", StringComparison.Ordinal), "Info.plist is wrong.");
                    string uninstall = MacBundleFiles.UninstallScript(manifest.Project, client);
                    Check(uninstall.Contains("rm -rf \"/Applications/Xeno.app\"", StringComparison.Ordinal) && uninstall.Contains("pkgutil --forget ai.xenocloud.xeno.pkg", StringComparison.Ordinal) && !uninstall.Contains(".xeno\"", StringComparison.Ordinal), "The uninstall script must remove the app and receipt and keep ~/.xeno.");
                    ProjectSettings tricky = new ProjectSettings { DisplayName = "A & B", Tagline = "<b>bold</b>", BundleId = "x.y" };
                    string welcome = MacBundleFiles.WelcomeHtml(tricky, client, "1.0.0");
                    Check(welcome.Contains("A &amp; B", StringComparison.Ordinal) && welcome.Contains("&lt;b&gt;bold&lt;/b&gt;", StringComparison.Ordinal), "The welcome page must escape the product text.");
                    Check(InstallerText.GatekeeperNote(manifest.Project).Contains("Open Anyway", StringComparison.Ordinal) && InstallerText.GatekeeperNote(manifest.Project).Contains("xattr -dr com.apple.quarantine /Applications/Xeno.app", StringComparison.Ordinal), "The disk image note must explain the prompt.");
                    return Task.CompletedTask;
                }),
                new TestCaseDescriptor(suite, "LinuxPackageFiles", "Menu and autostart entries, the AppImage entry point, and the remove script", executeAsync: delegate
                {
                    PublisherManifest manifest = Manifest();
                    ArtifactSettings client = Client(manifest);
                    ShortcutSettings terminal = client.Shortcuts.Single(item => item.DisplayName == "Xeno Terminal");
                    string entry = LinuxFiles.DesktopEntry(manifest.Project, terminal, "/opt/xeno/xeno");
                    Check(entry.Contains("Name=Xeno Terminal\n", StringComparison.Ordinal) && entry.Contains("Exec=/opt/xeno/xeno tui\n", StringComparison.Ordinal) && entry.Contains("Terminal=true\n", StringComparison.Ordinal) && entry.Contains("Icon=xeno\n", StringComparison.Ordinal), "The terminal menu entry is wrong: " + entry);
                    string autostart = LinuxFiles.AutostartEntry(manifest.Project, client.LoginItem!, "/opt/xeno/xeno-tray");
                    Check(autostart.Contains("Name=Xeno Agent\n", StringComparison.Ordinal) && autostart.Contains("Exec=/opt/xeno/xeno-tray\n", StringComparison.Ordinal), "The autostart entry is wrong.");
                    string appRun = LinuxFiles.AppRun(client, manifest.Channels["appimage"].Options["commands"]);
                    Check(appRun.Contains("  tui) shift; exec \"$HERE/usr/bin/xeno\" tui \"$@\" ;;", StringComparison.Ordinal) && appRun.Contains("  tray) shift; exec \"$HERE/usr/bin/xeno-tray\" \"$@\" ;;", StringComparison.Ordinal) && appRun.TrimEnd().EndsWith("exec \"$HERE/usr/bin/xeno-desktop\" \"$@\"", StringComparison.Ordinal), "AppRun is wrong: " + appRun);
                    string remove = LinuxFiles.BeforeRemove(client);
                    Check(remove.Contains("pkill -x xeno-tray", StringComparison.Ordinal) && remove.Contains("remove|purge|0) pkill -x xeno ", StringComparison.Ordinal), "The remove script must end the apps, and the client only on removal.");
                    Check(LinuxFiles.InstallDir(manifest.Project) == "/opt/xeno", "Linux installs to /opt/xeno.");
                    Check(client.LinuxDependencies.Deb.Any(item => item.StartsWith("libicu", StringComparison.Ordinal)) && client.LinuxDependencies.Deb.Contains("libx11-6") && client.LinuxDependencies.Rpm.Contains("fontconfig"), "The packages must declare ICU and the Avalonia libraries.");
                    return Task.CompletedTask;
                }),
                new TestCaseDescriptor(suite, "ImageAssets", "Icons and Inno Setup wizard bitmaps are generated from the logo at the requested sizes", executeAsync: delegate
                {
                    string logo = Path.Combine(RepositoryRoot(), Manifest().Project.Logo);
                    string directory = Path.Combine(Path.GetTempPath(), "xeno-publisher-" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(directory);
                    try
                    {
                        string png = Path.Combine(directory, "icon.png");
                        ImageAssets.SquarePng(logo, 48, png);
                        using (SKBitmap? bitmap = SKBitmap.Decode(png))
                        {
                            Check(bitmap != null && bitmap.Width == 48 && bitmap.Height == 48, "The 48 px icon has the wrong size.");
                        }

                        string bmp = Path.Combine(directory, "wizard.bmp");
                        ImageAssets.Banner(logo, 165, 314, 0.7, bmp);
                        byte[] bytes = File.ReadAllBytes(bmp);
                        int rowBytes = (165 * 3 + 3) & ~3;
                        Check(bytes[0] == (byte)'B' && bytes[1] == (byte)'M' && BitConverter.ToInt32(bytes, 18) == 165 && BitConverter.ToInt32(bytes, 22) == 314 && BitConverter.ToInt16(bytes, 28) == 24, "The wizard bitmap header is wrong.");
                        Check(bytes.Length == 54 + rowBytes * 314 && BitConverter.ToInt32(bytes, 2) == bytes.Length, "The wizard bitmap size or row padding is wrong.");
                        Check(bytes[54] == 255 && bytes[55] == 255 && bytes[56] == 255, "The wizard bitmap background must be white.");
                    }
                    finally
                    {
                        Directory.Delete(directory, true);
                    }

                    return Task.CompletedTask;
                }),
                new TestCaseDescriptor(suite, "ChecksumsAndOptions", "SHA256SUMS lists every file in name order; options and runtime selection parse as documented", executeAsync: delegate
                {
                    string directory = Path.Combine(Path.GetTempPath(), "xeno-publisher-" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(directory);
                    try
                    {
                        File.WriteAllText(Path.Combine(directory, "b.txt"), "b");
                        File.WriteAllText(Path.Combine(directory, "a.txt"), "a");
                        string sums = File.ReadAllText(Checksums.WriteSums(directory));
                        Check(sums == "ca978112ca1bbdcafac231b39a23dc4da786eff8147c4e72b9807785afee48bb  a.txt\n3e23e8160039594a33894f6564e1b1348bbd7a0088d42c4acb73eeaed59c009d  b.txt\n", "SHA256SUMS is wrong: " + sums);
                        Checksums.WriteSums(directory);
                        Check(!File.ReadAllText(Path.Combine(directory, Checksums.SumsFileName)).Contains(Checksums.SumsFileName, StringComparison.Ordinal), "SHA256SUMS must not list itself.");
                    }
                    finally
                    {
                        Directory.Delete(directory, true);
                    }

                    PublisherOptions options = PublisherOptions.Parse(new[] { "--channel", "debrpm, appimage", "--channel", "archive,debrpm", "--sums", "--version", "9.9.9" });
                    Check(options.Channels.SequenceEqual(new[] { "debrpm", "appimage", "archive" }) && options.Sums && options.Version == "9.9.9", "Options did not parse.");
                    ExpectThrows(() => PublisherOptions.Parse(new[] { "--channel" }));
                    ExpectThrows(() => PublisherOptions.Parse(new[] { "--bogus" }));
                    Check(PublisherOptions.Parse(Array.Empty<string>()).Help, "No arguments must print help.");

                    PublisherManifest manifest = Manifest();
                    Check(Program.ResolveTargets(manifest, manifest.Channels["inno"], new InnoChannel(), null).Select(item => item.Rid).SequenceEqual(new[] { "win-x64", "win-arm64" }), "inno must build both Windows runtimes.");
                    Check(Program.ResolveTargets(manifest, manifest.Channels["debrpm"], new DebRpmChannel(), "linux-arm64").Single().Rid == "linux-arm64", "--rid must override the channel's runtimes.");
                    Check(Program.ResolveTargets(manifest, manifest.Channels["archive"], new ArchiveChannel(), null).All(item => item.Os == RuntimeTarget.HostOs()), "archive must build the host's runtimes.");
                    List<string> installers = Program.ExpandChannels(new[] { "installers", "archive" });
                    Check(installers.SequenceEqual(ChannelRegistry.InstallerChannelsFor(RuntimeTarget.HostOs())), "'installers' must expand to the host's installer channels once each.");
                    return Task.CompletedTask;
                })
            });
        }

        private static PublisherManifest Manifest()
        {
            return PublisherManifest.Load(Path.Combine(RepositoryRoot(), "publisher.json"));
        }

        private static ArtifactSettings Client(PublisherManifest manifest)
        {
            return manifest.FindArtifact("client") ?? throw new InvalidOperationException("publisher.json has no client artifact.");
        }

        private static string RepositoryRoot()
        {
            DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "src", "Xeno.sln"))) return directory.FullName;
                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Unable to locate the repository root containing src/Xeno.sln.");
        }

        private static void ExpectThrows(Action action)
        {
            try
            {
                action();
            }
            catch (ArgumentException)
            {
                return;
            }

            throw new InvalidOperationException("Expected an ArgumentException.");
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}

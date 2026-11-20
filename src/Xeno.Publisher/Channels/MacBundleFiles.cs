namespace Xeno.Publisher.Channels
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Text;
    using Xeno.Publisher.Manifest;

    /// <summary>
    /// Renders the text files of the macOS app bundle and installer package: Info.plist, the LaunchAgent, the
    /// package scripts, the uninstall script, the distribution file, and the welcome page.
    /// </summary>
    public static class MacBundleFiles
    {
        /// <summary>
        /// The installed app bundle path, for example "/Applications/Xeno.app".
        /// </summary>
        /// <param name="project">Project settings.</param>
        /// <returns>Absolute path on the target Mac.</returns>
        public static string InstalledApp(ProjectSettings project)
        {
            ArgumentNullException.ThrowIfNull(project);
            return "/Applications/" + project.DisplayName + ".app";
        }

        /// <summary>
        /// The installer package identifier, which pkgutil and uninstall use: the bundle id plus ".pkg".
        /// </summary>
        /// <param name="project">Project settings.</param>
        /// <returns>Package identifier.</returns>
        public static string PackageId(ProjectSettings project)
        {
            ArgumentNullException.ThrowIfNull(project);
            return project.BundleId + ".pkg";
        }

        /// <summary>
        /// The LaunchAgent label for the login item, or an empty string when there is none.
        /// </summary>
        /// <param name="project">Project settings.</param>
        /// <param name="artifact">Artifact.</param>
        /// <returns>Label, for example "ai.xenocloud.xeno.agent".</returns>
        public static string AgentLabel(ProjectSettings project, ArtifactSettings artifact)
        {
            ArgumentNullException.ThrowIfNull(project);
            ArgumentNullException.ThrowIfNull(artifact);
            return artifact.LoginItem == null ? "" : project.BundleId + "." + artifact.LoginItem.LabelSuffix;
        }

        /// <summary>
        /// Info.plist for the app bundle.
        /// </summary>
        /// <param name="project">Project settings.</param>
        /// <param name="artifact">Artifact.</param>
        /// <param name="version">Version.</param>
        /// <returns>Plist XML.</returns>
        public static string InfoPlist(ProjectSettings project, ArtifactSettings artifact, string version)
        {
            ArgumentNullException.ThrowIfNull(project);
            ArgumentNullException.ThrowIfNull(artifact);
            return
                PlistHeader +
                "<dict>\n" +
                Entry("CFBundleName", project.DisplayName) +
                Entry("CFBundleDisplayName", project.DisplayName) +
                Entry("CFBundleIdentifier", project.BundleId) +
                Entry("CFBundleExecutable", artifact.MainExecutable) +
                Entry("CFBundleIconFile", "AppIcon") +
                Entry("CFBundlePackageType", "APPL") +
                Entry("CFBundleShortVersionString", version) +
                Entry("CFBundleVersion", version) +
                Entry("CFBundleInfoDictionaryVersion", "6.0") +
                Entry("LSMinimumSystemVersion", "13.0") +
                Entry("LSApplicationCategoryType", "public.app-category.developer-tools") +
                Entry("NSHumanReadableCopyright", "Copyright " + project.Publisher + ". " + project.License + " License.") +
                "  <key>NSHighResolutionCapable</key>\n  <true/>\n" +
                Entry("NSPrincipalClass", "NSApplication") +
                "</dict>\n</plist>\n";
        }

        /// <summary>
        /// LaunchAgent plist that starts the login item for each user who logs in. It is not kept alive, so quitting
        /// the agent sticks until the next login or until an app starts it again.
        /// </summary>
        /// <param name="project">Project settings.</param>
        /// <param name="artifact">Artifact with a login item.</param>
        /// <returns>Plist XML.</returns>
        /// <exception cref="ArgumentException">The artifact has no login item.</exception>
        public static string LaunchAgentPlist(ProjectSettings project, ArtifactSettings artifact)
        {
            ArgumentNullException.ThrowIfNull(project);
            ArgumentNullException.ThrowIfNull(artifact);
            if (artifact.LoginItem == null) throw new ArgumentException("The artifact has no login item.", nameof(artifact));
            string program = InstalledApp(project) + "/Contents/MacOS/" + artifact.LoginItem.Executable;
            return
                PlistHeader +
                "<dict>\n" +
                Entry("Label", AgentLabel(project, artifact)) +
                "  <key>ProgramArguments</key>\n  <array>\n    <string>" + Escape(program) + "</string>\n  </array>\n" +
                "  <key>RunAtLoad</key>\n  <true/>\n" +
                Entry("ProcessType", "Interactive") +
                Entry("LimitLoadToSessionType", "Aqua") +
                "</dict>\n</plist>\n";
        }

        /// <summary>
        /// Package preinstall script: stops the running product so the bundle can be replaced.
        /// </summary>
        /// <param name="project">Project settings.</param>
        /// <param name="artifact">Artifact.</param>
        /// <returns>Shell script.</returns>
        public static string PreinstallScript(ProjectSettings project, ArtifactSettings artifact)
        {
            ArgumentNullException.ThrowIfNull(project);
            ArgumentNullException.ThrowIfNull(artifact);
            StringBuilder s = new StringBuilder();
            s.Append("#!/bin/sh\n# Generated by Xeno.Publisher. Stops the running ").Append(project.DisplayName).Append(" so its files can be replaced.\n");
            s.Append(ConsoleUser);
            s.Append(StopLines(project, artifact));
            s.Append("exit 0\n");
            return s.ToString();
        }

        /// <summary>
        /// Package postinstall script: links the PATH commands into /usr/local/bin and installs and starts the
        /// LaunchAgent for the logged-in user.
        /// </summary>
        /// <param name="project">Project settings.</param>
        /// <param name="artifact">Artifact.</param>
        /// <returns>Shell script.</returns>
        public static string PostinstallScript(ProjectSettings project, ArtifactSettings artifact)
        {
            ArgumentNullException.ThrowIfNull(project);
            ArgumentNullException.ThrowIfNull(artifact);
            string app = InstalledApp(project);
            StringBuilder s = new StringBuilder();
            s.Append("#!/bin/sh\n# Generated by Xeno.Publisher.\n");
            if (artifact.PathCommands.Count > 0)
            {
                s.Append("/bin/mkdir -p /usr/local/bin\n");
                foreach (string command in artifact.PathCommands) s.Append("/bin/ln -sf \"").Append(app).Append("/Contents/MacOS/").Append(command).Append("\" /usr/local/bin/").Append(command).Append('\n');
            }

            if (artifact.LoginItem != null)
            {
                string label = AgentLabel(project, artifact);
                string agent = "/Library/LaunchAgents/" + label + ".plist";
                s.Append(ConsoleUser);
                s.Append("/bin/cp \"").Append(app).Append("/Contents/Resources/").Append(label).Append(".plist\" \"").Append(agent).Append("\"\n");
                s.Append("/usr/sbin/chown root:wheel \"").Append(agent).Append("\"\n");
                s.Append("/bin/chmod 644 \"").Append(agent).Append("\"\n");
                s.Append("if [ -n \"$uid\" ]; then\n");
                s.Append("  /bin/launchctl bootout \"gui/$uid/").Append(label).Append("\" 2>/dev/null || true\n");
                s.Append("  /bin/launchctl bootstrap \"gui/$uid\" \"").Append(agent).Append("\" 2>/dev/null || true\n");
                s.Append("fi\n");
            }

            s.Append("exit 0\n");
            return s.ToString();
        }

        /// <summary>
        /// Uninstall script shipped in the bundle's Resources folder. Run with sudo; it leaves each user's settings
        /// (for Xeno, ~/.xeno) alone.
        /// </summary>
        /// <param name="project">Project settings.</param>
        /// <param name="artifact">Artifact.</param>
        /// <returns>Shell script.</returns>
        public static string UninstallScript(ProjectSettings project, ArtifactSettings artifact)
        {
            ArgumentNullException.ThrowIfNull(project);
            ArgumentNullException.ThrowIfNull(artifact);
            StringBuilder s = new StringBuilder();
            s.Append("#!/bin/sh\n# Removes ").Append(project.DisplayName).Append(" installed by the .pkg. Run: sudo \"").Append(InstalledApp(project)).Append("/Contents/Resources/uninstall.sh\"\n");
            s.Append("# Each user's settings are kept.\n");
            s.Append("if [ \"$(/usr/bin/id -u)\" != 0 ]; then echo \"Run with sudo.\" >&2; exit 1; fi\n");
            s.Append(ConsoleUser);
            s.Append(StopLines(project, artifact));
            if (artifact.LoginItem != null) s.Append("/bin/rm -f \"/Library/LaunchAgents/").Append(AgentLabel(project, artifact)).Append(".plist\"\n");
            foreach (string command in artifact.PathCommands)
            {
                s.Append("if [ \"$(/usr/bin/readlink /usr/local/bin/").Append(command).Append(")\" = \"").Append(InstalledApp(project)).Append("/Contents/MacOS/").Append(command).Append("\" ]; then /bin/rm -f /usr/local/bin/").Append(command).Append("; fi\n");
            }

            s.Append("/bin/rm -rf \"").Append(InstalledApp(project)).Append("\"\n");
            s.Append("/usr/sbin/pkgutil --forget ").Append(PackageId(project)).Append(" >/dev/null 2>&1 || true\n");
            s.Append("echo \"").Append(project.DisplayName).Append(" was removed.\"\n");
            return s.ToString();
        }

        /// <summary>
        /// productbuild distribution file: title, welcome page, license (which the user must accept), background,
        /// and the single component.
        /// </summary>
        /// <param name="project">Project settings.</param>
        /// <param name="version">Version.</param>
        /// <param name="target">Runtime target.</param>
        /// <param name="componentFile">Component package file name.</param>
        /// <returns>Distribution XML.</returns>
        public static string DistributionXml(ProjectSettings project, string version, RuntimeTarget target, string componentFile)
        {
            ArgumentNullException.ThrowIfNull(project);
            ArgumentNullException.ThrowIfNull(target);
            string id = PackageId(project);
            return
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<installer-gui-script minSpecVersion=\"2\">\n" +
                "  <title>" + Escape(project.DisplayName) + "</title>\n" +
                "  <welcome file=\"welcome.html\" mime-type=\"text/html\"/>\n" +
                "  <license file=\"LICENSE.txt\" mime-type=\"text/plain\"/>\n" +
                "  <background file=\"background.png\" mime-type=\"image/png\" alignment=\"bottomleft\" scaling=\"none\"/>\n" +
                "  <background-darkAqua file=\"background.png\" mime-type=\"image/png\" alignment=\"bottomleft\" scaling=\"none\"/>\n" +
                "  <options customize=\"never\" require-scripts=\"false\" hostArchitectures=\"" + target.MacHostArchitectures + "\"/>\n" +
                "  <domains enable_anywhere=\"false\" enable_currentUserHome=\"false\" enable_localSystem=\"true\"/>\n" +
                "  <choices-outline>\n    <line choice=\"default\"/>\n  </choices-outline>\n" +
                "  <choice id=\"default\" title=\"" + Escape(project.DisplayName) + "\">\n    <pkg-ref id=\"" + id + "\"/>\n  </choice>\n" +
                "  <pkg-ref id=\"" + id + "\" version=\"" + Escape(version) + "\" onConclusion=\"none\">" + Escape(componentFile) + "</pkg-ref>\n" +
                "</installer-gui-script>\n";
        }

        /// <summary>
        /// The installer's welcome page: product name, tagline, and what gets installed.
        /// </summary>
        /// <param name="project">Project settings.</param>
        /// <param name="artifact">Artifact.</param>
        /// <param name="version">Version.</param>
        /// <returns>HTML.</returns>
        public static string WelcomeHtml(ProjectSettings project, ArtifactSettings artifact, string version)
        {
            ArgumentNullException.ThrowIfNull(project);
            List<string> items = InstallerText.WhatGetsInstalled(project, artifact, OsFamilyEnum.MacOs);
            StringBuilder s = new StringBuilder();
            s.Append("<!DOCTYPE html>\n<html><head><meta charset=\"utf-8\"><style>body{font-family:-apple-system,Helvetica,sans-serif;font-size:13px;margin:0 0 0 0;} h1{font-size:18px;margin:0 0 8px 0;} ul{padding-left:18px;}</style></head><body>\n");
            s.Append("<h1>").Append(WebUtility.HtmlEncode(project.DisplayName)).Append(' ').Append(WebUtility.HtmlEncode(version)).Append("</h1>\n");
            s.Append("<p>").Append(WebUtility.HtmlEncode(project.Tagline)).Append("</p>\n");
            s.Append("<p>This installs:</p>\n<ul>\n");
            foreach (string item in items) s.Append("<li>").Append(WebUtility.HtmlEncode(item)).Append("</li>\n");
            s.Append("</ul>\n");
            s.Append("<p>To remove it later, run <code>sudo \"").Append(WebUtility.HtmlEncode(InstalledApp(project))).Append("/Contents/Resources/uninstall.sh\"</code>.</p>\n");
            s.Append("</body></html>\n");
            return s.ToString();
        }

        private const string PlistHeader =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
            "<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n" +
            "<plist version=\"1.0\">\n";

        private const string ConsoleUser =
            "console_user=$(/usr/bin/stat -f%Su /dev/console 2>/dev/null || true)\n" +
            "uid=\"\"\n" +
            "if [ -n \"$console_user\" ] && [ \"$console_user\" != root ] && [ \"$console_user\" != loginwindow ]; then\n" +
            "  uid=$(/usr/bin/id -u \"$console_user\" 2>/dev/null || true)\n" +
            "fi\n";

        private static string StopLines(ProjectSettings project, ArtifactSettings artifact)
        {
            string app = InstalledApp(project);
            StringBuilder s = new StringBuilder();
            if (artifact.StopCommand != null)
            {
                string exe = app + "/Contents/MacOS/" + artifact.StopCommand.Executable;
                s.Append("if [ -n \"$uid\" ] && [ -x \"").Append(exe).Append("\" ]; then\n");
                s.Append("  /usr/bin/sudo -H -u \"$console_user\" \"").Append(exe).Append('"');
                foreach (string argument in artifact.StopCommand.Arguments) s.Append(' ').Append(argument);
                s.Append(" >/dev/null 2>&1 || true\nfi\n");
            }

            if (artifact.LoginItem != null) s.Append("if [ -n \"$uid\" ]; then /bin/launchctl bootout \"gui/$uid/").Append(AgentLabel(project, artifact)).Append("\" 2>/dev/null || true; fi\n");
            foreach (string process in artifact.Processes) s.Append("/usr/bin/pkill -x ").Append(process).Append(" 2>/dev/null || true\n");
            return s.ToString();
        }

        private static string Entry(string key, string value)
        {
            return "  <key>" + key + "</key>\n  <string>" + Escape(value) + "</string>\n";
        }

        private static string Escape(string value)
        {
            return WebUtility.HtmlEncode(value ?? "");
        }
    }
}

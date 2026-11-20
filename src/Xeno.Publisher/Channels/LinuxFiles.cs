namespace Xeno.Publisher.Channels
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Xeno.Publisher.Manifest;

    /// <summary>
    /// Renders the Linux package files: application menu entries, the XDG autostart entry, and the package scripts.
    /// </summary>
    public static class LinuxFiles
    {
        /// <summary>
        /// Icon sizes installed under /usr/share/icons/hicolor.
        /// </summary>
        public static IReadOnlyList<int> IconSizes { get; } = new[] { 48, 64, 128, 256, 512 };

        /// <summary>
        /// Where the packages install the payload, for example "/opt/xeno".
        /// </summary>
        /// <param name="project">Project settings.</param>
        /// <returns>Absolute path.</returns>
        public static string InstallDir(ProjectSettings project)
        {
            ArgumentNullException.ThrowIfNull(project);
            return "/opt/" + project.Name;
        }

        /// <summary>
        /// A desktop entry for a menu shortcut.
        /// </summary>
        /// <param name="project">Project settings.</param>
        /// <param name="shortcut">Shortcut.</param>
        /// <param name="exec">Exec value, for example "/opt/xeno/xeno-desktop".</param>
        /// <returns>Desktop entry text.</returns>
        public static string DesktopEntry(ProjectSettings project, ShortcutSettings shortcut, string exec)
        {
            ArgumentNullException.ThrowIfNull(project);
            ArgumentNullException.ThrowIfNull(shortcut);
            StringBuilder s = new StringBuilder();
            s.Append("[Desktop Entry]\n");
            s.Append("Type=Application\n");
            s.Append("Name=").Append(shortcut.DisplayName).Append('\n');
            s.Append("Comment=").Append(String.IsNullOrEmpty(shortcut.Comment) ? project.Tagline : shortcut.Comment).Append('\n');
            s.Append("Exec=").Append(exec).Append(String.IsNullOrEmpty(shortcut.Arguments) ? "" : " " + shortcut.Arguments).Append('\n');
            s.Append("Icon=").Append(project.Name).Append('\n');
            s.Append("Terminal=").Append(shortcut.Terminal ? "true" : "false").Append('\n');
            s.Append("Categories=Network;Utility;\n");
            return s.ToString();
        }

        /// <summary>
        /// The XDG autostart entry that starts the login item with each user's desktop session.
        /// </summary>
        /// <param name="project">Project settings.</param>
        /// <param name="loginItem">Login item.</param>
        /// <param name="exec">Exec value.</param>
        /// <returns>Desktop entry text.</returns>
        public static string AutostartEntry(ProjectSettings project, LoginItemSettings loginItem, string exec)
        {
            ArgumentNullException.ThrowIfNull(project);
            ArgumentNullException.ThrowIfNull(loginItem);
            return
                "[Desktop Entry]\n" +
                "Type=Application\n" +
                "Name=" + loginItem.DisplayName + "\n" +
                "Comment=" + (String.IsNullOrEmpty(loginItem.Comment) ? project.Tagline : loginItem.Comment) + "\n" +
                "Exec=" + exec + "\n" +
                "Icon=" + project.Name + "\n" +
                "Terminal=false\n" +
                "X-GNOME-Autostart-enabled=true\n";
        }

        /// <summary>
        /// The post-install script: refreshes the icon cache and the menu database.
        /// </summary>
        /// <returns>Shell script.</returns>
        public static string AfterInstall()
        {
            return
                "#!/bin/sh\n" +
                "gtk-update-icon-cache -q -t /usr/share/icons/hicolor 2>/dev/null || true\n" +
                "update-desktop-database -q /usr/share/applications 2>/dev/null || true\n" +
                "exit 0\n";
        }

        /// <summary>
        /// The pre-remove script: ends the apps so their files can be replaced, and on removal (not upgrade) also ends
        /// the stop command's program, which runs the client. Each user's settings are left alone.
        /// </summary>
        /// <param name="artifact">Artifact.</param>
        /// <returns>Shell script.</returns>
        public static string BeforeRemove(ArtifactSettings artifact)
        {
            ArgumentNullException.ThrowIfNull(artifact);
            StringBuilder s = new StringBuilder("#!/bin/sh\n");
            foreach (string process in artifact.Processes) s.Append("pkill -x ").Append(process).Append(" 2>/dev/null || true\n");
            if (artifact.StopCommand != null)
            {
                s.Append("case \"$1\" in\n  remove|purge|0) pkill -x ").Append(artifact.StopCommand.Executable).Append(" 2>/dev/null || true ;;\nesac\n");
            }

            s.Append("exit 0\n");
            return s.ToString();
        }

        /// <summary>
        /// The AppImage entry point. A leading subcommand picks a bundled program; with none it opens the main
        /// executable.
        /// </summary>
        /// <param name="artifact">Artifact.</param>
        /// <param name="commands">Subcommands as "name=executable [arguments]" pairs separated by semicolons.</param>
        /// <returns>Shell script.</returns>
        public static string AppRun(ArtifactSettings artifact, string commands)
        {
            ArgumentNullException.ThrowIfNull(artifact);
            StringBuilder s = new StringBuilder("#!/bin/sh\nHERE=\"$(dirname \"$(readlink -f \"$0\")\")\"\n");
            List<string[]> pairs = (commands ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(item => item.Split('=', 2, StringSplitOptions.TrimEntries))
                .Where(item => item.Length == 2 && item[0].Length > 0 && item[1].Length > 0)
                .ToList();
            if (pairs.Count > 0)
            {
                s.Append("case \"$1\" in\n");
                foreach (string[] pair in pairs)
                {
                    string[] parts = pair[1].Split(' ', 2, StringSplitOptions.TrimEntries);
                    s.Append("  ").Append(pair[0]).Append(") shift; exec \"$HERE/usr/bin/").Append(parts[0]).Append('"');
                    if (parts.Length == 2) s.Append(' ').Append(parts[1]);
                    s.Append(" \"$@\" ;;\n");
                }

                s.Append("esac\n");
            }

            s.Append("exec \"$HERE/usr/bin/").Append(artifact.MainExecutable).Append("\" \"$@\"\n");
            return s.ToString();
        }
    }
}

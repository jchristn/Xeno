namespace Xeno.Publisher
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Xeno.Publisher.Manifest;

    /// <summary>
    /// Text every installer shows: what the product is and what the installer puts on the machine, and the note on
    /// getting past the unsigned-build prompt. Kept in one place so the formats say the same thing.
    /// </summary>
    public static class InstallerText
    {
        /// <summary>
        /// What an installer adds, one item per line, for the given operating system.
        /// </summary>
        /// <param name="project">Project settings.</param>
        /// <param name="artifact">Artifact being installed.</param>
        /// <param name="os">Operating system.</param>
        /// <returns>Items such as "The xeno command, added to PATH".</returns>
        public static List<string> WhatGetsInstalled(ProjectSettings project, ArtifactSettings artifact, OsFamilyEnum os)
        {
            ArgumentNullException.ThrowIfNull(project);
            ArgumentNullException.ThrowIfNull(artifact);
            List<string> items = new List<string>();
            if (os == OsFamilyEnum.MacOs)
            {
                items.Add(project.DisplayName + ".app in Applications");
            }
            else
            {
                List<string> names = artifact.Shortcuts.Select(item => item.DisplayName).ToList();
                string where = os == OsFamilyEnum.Windows ? "the Start menu" : "the application menu";
                if (names.Count > 0) items.Add(JoinNames(names) + " in " + where);
            }

            foreach (string command in artifact.PathCommands)
            {
                items.Add(os == OsFamilyEnum.MacOs ? "The " + command + " command in /usr/local/bin" : "The " + command + " command, added to PATH");
            }

            if (artifact.LoginItem != null)
            {
                string when = os == OsFamilyEnum.Windows ? "when you sign in (optional)" : "when you log in";
                items.Add(artifact.LoginItem.DisplayName + ", started " + when);
            }

            return items;
        }

        /// <summary>
        /// The welcome paragraph: the tagline followed by what gets installed.
        /// </summary>
        /// <param name="project">Project settings.</param>
        /// <param name="artifact">Artifact being installed.</param>
        /// <param name="os">Operating system.</param>
        /// <returns>Plain text, one sentence per item.</returns>
        public static string Welcome(ProjectSettings project, ArtifactSettings artifact, OsFamilyEnum os)
        {
            ArgumentNullException.ThrowIfNull(project);
            List<string> items = WhatGetsInstalled(project, artifact, os);
            return project.Tagline + " This installs: " + String.Join("; ", items.Select(item => LowerFirst(item))) + ".";
        }

        /// <summary>
        /// The note placed beside the app in a macOS disk image, with the steps past Gatekeeper's prompt.
        /// </summary>
        /// <param name="project">Project settings.</param>
        /// <returns>Plain text.</returns>
        public static string GatekeeperNote(ProjectSettings project)
        {
            ArgumentNullException.ThrowIfNull(project);
            string name = project.DisplayName;
            return
                "IF MACOS SAYS \"" + name + "\" NOT OPENED\n\n" +
                name + " is free, open-source software and is not code-signed (signing certificates are an ongoing cost\n" +
                "we choose not to pass on). The first time you open it, macOS says:\n\n" +
                "    \"" + name + "\" Not Opened\n" +
                "    Apple could not verify \"" + name + "\" is free of malware that may harm your Mac or\n" +
                "    compromise your privacy.\n\n" +
                "That doesn't mean the download is damaged. To open it anyway:\n\n" +
                "  1. Drag " + name + ".app onto the Applications folder in this window, then open it from there.\n" +
                "  2. When the message appears, click Done. Do not click Move to Trash.\n" +
                "  3. Open System Settings > Privacy & Security and scroll down to Security.\n" +
                "  4. Next to \"" + name + "\" was blocked to protect your Mac, click Open Anyway. The button only\n" +
                "     appears for about an hour after step 2; if it is missing, try to open " + name + " again first.\n" +
                "  5. Click Open Anyway again in the dialog that follows and enter your password. macOS remembers\n" +
                "     the choice, so you only do this once.\n\n" +
                "Control-click > Open no longer gets past this prompt on macOS 15 and later.\n\n" +
                "Or, after step 1, run this in Terminal and open " + name + " normally:\n\n" +
                "    xattr -dr com.apple.quarantine /Applications/" + name + ".app\n\n" +
                "The .pkg installer also puts the " + project.Name + " command on your PATH and starts the agent at login, which\n" +
                "the drag-and-drop copy does not. Full instructions: " + project.Repo + "/blob/main/INSTALL.md\n";
        }

        private static string JoinNames(List<string> names)
        {
            if (names.Count == 1) return names[0];
            return String.Join(", ", names.Take(names.Count - 1)) + " and " + names[names.Count - 1];
        }

        private static string LowerFirst(string text)
        {
            if (text.StartsWith("The ", StringComparison.Ordinal)) return "the " + text.Substring(4);
            return text;
        }
    }
}

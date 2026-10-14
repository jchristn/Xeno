namespace Xeno.Client.Hosting
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;

    /// <summary>
    /// Plans how to open a new terminal window running a command, and how to open URLs and folders, per platform.
    /// Pure functions: nothing here starts a process. Thread-safe.
    /// </summary>
    public static class TerminalLaunchPlanner
    {
        #region Public-Methods

        /// <summary>
        /// Platform of the current process.
        /// </summary>
        /// <returns>Platform.</returns>
        public static TerminalPlatformEnum CurrentPlatform()
        {
            if (OperatingSystem.IsWindows()) return TerminalPlatformEnum.Windows;
            if (OperatingSystem.IsMacOS()) return TerminalPlatformEnum.MacOS;
            return TerminalPlatformEnum.Linux;
        }

        /// <summary>
        /// Plan the commands that open a terminal window running an executable. Linux returns several candidates to try
        /// in order (x-terminal-emulator, gnome-terminal, konsole, xfce4-terminal, kitty, alacritty, xterm); other
        /// platforms return one. The window stays open with an interactive shell after the command exits.
        /// </summary>
        /// <param name="platform">Target platform.</param>
        /// <param name="location">Executable to run.</param>
        /// <param name="arguments">Arguments after the location's leading arguments.</param>
        /// <param name="workingDirectory">Working directory for the command.</param>
        /// <param name="title">Window title (Windows).</param>
        /// <param name="scriptDirectory">Directory for the temporary macOS .command script.</param>
        /// <returns>Candidate launch commands, in the order to try.</returns>
        /// <exception cref="ArgumentNullException">Location is null.</exception>
        public static List<LaunchCommand> PlanTerminal(
            TerminalPlatformEnum platform,
            XenoExecutableLocation location,
            IEnumerable<string>? arguments,
            string workingDirectory,
            string title,
            string scriptDirectory)
        {
            ArgumentNullException.ThrowIfNull(location);
            List<string> args = arguments == null ? new List<string>() : new List<string>(arguments);
            List<LaunchCommand> commands = new List<LaunchCommand>();
            if (platform == TerminalPlatformEnum.Windows)
            {
                string command = location.ToWindowsCommand(args);
                commands.Add(new LaunchCommand
                {
                    FileName = "cmd.exe",
                    RawArguments = "/c start \"" + SanitizeTitle(title) + "\" /d " + QuoteWindows(workingDirectory) + " cmd.exe /k " + command,
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
                return commands;
            }

            string shellCommand = location.ToShellCommand(args);
            if (platform == TerminalPlatformEnum.MacOS)
            {
                string scriptPath = Path.Combine(scriptDirectory, "xeno-terminal-" + Guid.NewGuid().ToString("N") + ".command");
                commands.Add(new LaunchCommand
                {
                    FileName = "/usr/bin/open",
                    Arguments = new List<string> { "-a", "Terminal", scriptPath },
                    ScriptPath = scriptPath,
                    ScriptContent = BuildMacCommandFile(shellCommand, workingDirectory)
                });
                return commands;
            }

            string inner = "cd " + QuotePosix(workingDirectory) + " 2>/dev/null; " + shellCommand + "; exec \"${SHELL:-/bin/sh}\" -l";
            commands.Add(LinuxCommand("x-terminal-emulator", "-e", inner));
            commands.Add(LinuxCommand("gnome-terminal", "--", inner));
            commands.Add(LinuxCommand("konsole", "-e", inner));
            commands.Add(LinuxCommand("xfce4-terminal", "-x", inner));
            commands.Add(LinuxCommand("kitty", null, inner));
            commands.Add(LinuxCommand("alacritty", "-e", inner));
            commands.Add(LinuxCommand("xterm", "-e", inner));
            return commands;
        }

        /// <summary>
        /// Build the macOS .command script that Terminal.app runs. The script deletes itself, changes to the working
        /// directory, runs the command, then leaves an interactive login shell open.
        /// </summary>
        /// <param name="shellCommand">Quoted command line.</param>
        /// <param name="workingDirectory">Working directory.</param>
        /// <returns>Script contents.</returns>
        public static string BuildMacCommandFile(string shellCommand, string workingDirectory)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("#!/bin/zsh -l\n");
            builder.Append("rm -f \"$0\"\n");
            builder.Append("cd ").Append(QuotePosix(workingDirectory)).Append(" 2>/dev/null || cd \"$HOME\"\n");
            builder.Append("clear\n");
            builder.Append(shellCommand).Append('\n');
            builder.Append("exec \"${SHELL:-/bin/zsh}\" -l\n");
            return builder.ToString();
        }

        /// <summary>
        /// Plan opening a URL, file, or folder with the platform's default handler.
        /// </summary>
        /// <param name="platform">Target platform.</param>
        /// <param name="target">URL or path.</param>
        /// <returns>Launch command.</returns>
        /// <exception cref="ArgumentException">Target is empty.</exception>
        public static LaunchCommand PlanOpen(TerminalPlatformEnum platform, string target)
        {
            if (String.IsNullOrWhiteSpace(target)) throw new ArgumentException("A URL or path is required.", nameof(target));
            if (platform == TerminalPlatformEnum.Windows) return new LaunchCommand { FileName = target, UseShellExecute = true };
            if (platform == TerminalPlatformEnum.MacOS) return new LaunchCommand { FileName = "/usr/bin/open", Arguments = new List<string> { target } };
            return new LaunchCommand { FileName = "xdg-open", Arguments = new List<string> { target } };
        }

        /// <summary>
        /// Plan revealing a file in the platform file manager (Explorer, Finder, or the folder on Linux).
        /// </summary>
        /// <param name="platform">Target platform.</param>
        /// <param name="path">File or folder.</param>
        /// <returns>Launch command.</returns>
        /// <exception cref="ArgumentException">Path is empty.</exception>
        public static LaunchCommand PlanReveal(TerminalPlatformEnum platform, string path)
        {
            if (String.IsNullOrWhiteSpace(path)) throw new ArgumentException("A path is required.", nameof(path));
            if (platform == TerminalPlatformEnum.Windows)
            {
                return new LaunchCommand { FileName = "explorer.exe", RawArguments = "/select," + QuoteWindows(path) };
            }

            if (platform == TerminalPlatformEnum.MacOS) return new LaunchCommand { FileName = "/usr/bin/open", Arguments = new List<string> { "-R", path } };
            string folder = Directory.Exists(path) ? path : (Path.GetDirectoryName(path) ?? path);
            return new LaunchCommand { FileName = "xdg-open", Arguments = new List<string> { folder } };
        }

        /// <summary>
        /// Quote a value for a POSIX shell using single quotes.
        /// </summary>
        /// <param name="value">Value.</param>
        /// <returns>Quoted value.</returns>
        public static string QuotePosix(string? value)
        {
            string text = value ?? String.Empty;
            return "'" + text.Replace("'", "'\\''") + "'";
        }

        /// <summary>
        /// Quote a value for a Windows command line.
        /// </summary>
        /// <param name="value">Value.</param>
        /// <returns>Quoted value.</returns>
        public static string QuoteWindows(string? value)
        {
            string text = value ?? String.Empty;
            return "\"" + text.Replace("\"", "\\\"") + "\"";
        }

        #endregion

        #region Private-Methods

        private static LaunchCommand LinuxCommand(string terminal, string? executeFlag, string inner)
        {
            LaunchCommand command = new LaunchCommand { FileName = terminal };
            if (!String.IsNullOrWhiteSpace(executeFlag)) command.Arguments.Add(executeFlag);
            command.Arguments.Add("/bin/sh");
            command.Arguments.Add("-lc");
            command.Arguments.Add(inner);
            return command;
        }

        private static string SanitizeTitle(string title)
        {
            return String.IsNullOrWhiteSpace(title) ? "Xeno" : title.Replace("\"", String.Empty);
        }

        #endregion
    }
}

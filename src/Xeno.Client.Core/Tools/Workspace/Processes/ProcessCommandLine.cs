namespace Xeno.Client.Tools.Workspace.Processes
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Builds the process start for run_process and process_start. By default the command is a program and args are its
    /// arguments, passed as an argument list with no shell in between. With shell set, the command and its args are
    /// joined into one command line and run by the platform shell (<c>/bin/sh -c</c>, or <c>cmd.exe /c</c> on Windows),
    /// as Mux always does.
    /// </summary>
    internal static class ProcessCommandLine
    {
        #region Private-Members

        private static readonly bool _IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        #endregion

        #region Public-Members

        /// <summary>
        /// The platform shell.
        /// </summary>
        public static string ShellProgram
        {
            get { return _IsWindows ? "cmd.exe" : "/bin/sh"; }
        }

        /// <summary>
        /// How the platform shell is invoked.
        /// </summary>
        public static string ShellInvocation
        {
            get { return _IsWindows ? "cmd.exe /c <command>" : "/bin/sh -c \"<command>\""; }
        }

        /// <summary>
        /// The operating system, for tool descriptions.
        /// </summary>
        public static string OperatingSystemLabel
        {
            get { return RuntimeInformation.OSDescription.Trim(); }
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Build the start information.
        /// </summary>
        /// <param name="command">Program (or command line with shell).</param>
        /// <param name="args">Arguments, or null.</param>
        /// <param name="shell">Run through the platform shell.</param>
        /// <param name="workingDirectory">Full, existing working directory.</param>
        /// <returns>Start information with stdout and stderr redirected.</returns>
        /// <exception cref="ArgumentException">The command is blank, or is a command line without shell.</exception>
        public static ProcessStartInfo Create(string? command, IReadOnlyList<string>? args, bool shell, string workingDirectory)
        {
            if (String.IsNullOrWhiteSpace(command)) throw new ArgumentException("Required parameter 'command' is missing or not a string.");
            List<string> arguments = new List<string>();
            if (args != null)
            {
                foreach (string arg in args)
                {
                    if (arg != null) arguments.Add(arg);
                }
            }

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            if (shell)
            {
                string line = Display(command, arguments);
                if (_IsWindows)
                {
                    // cmd.exe does its own parsing and does not understand the escaping ArgumentList applies.
                    startInfo.FileName = "cmd.exe";
                    startInfo.Arguments = "/c " + line;
                }
                else
                {
                    startInfo.FileName = "/bin/sh";
                    startInfo.ArgumentList.Add("-c");
                    startInfo.ArgumentList.Add(line);
                }
            }
            else
            {
                string program = command.Trim();
                bool hasSeparator = program.IndexOf('/') >= 0 || (_IsWindows && program.IndexOf('\\') >= 0);
                if (hasSeparator && !Path.IsPathRooted(program))
                {
                    // A relative program path means relative to the process's working directory, not to Xeno's.
                    program = Path.GetFullPath(Path.Combine(workingDirectory, program));
                }

                if (arguments.Count == 0 && ContainsWhitespace(program) && !File.Exists(program))
                {
                    throw new ArgumentException("'command' is a program name, not a command line: pass its arguments in 'args', or set 'shell' to true to run a command line through " + ShellProgram + ".");
                }

                startInfo.FileName = program;
                foreach (string arg in arguments) startInfo.ArgumentList.Add(arg);
            }

            startInfo.Environment["NO_COLOR"] = "1";
            startInfo.Environment["FORCE_COLOR"] = "0";
            return startInfo;
        }

        /// <summary>
        /// The command and its arguments as one line, for display and for the shell.
        /// </summary>
        /// <param name="command">Command.</param>
        /// <param name="args">Arguments.</param>
        /// <returns>Command line.</returns>
        public static string Display(string command, IReadOnlyList<string>? args)
        {
            List<string> parts = new List<string> { command.Trim() };
            if (args != null)
            {
                foreach (string arg in args)
                {
                    if (arg != null) parts.Add(arg);
                }
            }

            return String.Join(" ", parts);
        }

        /// <summary>
        /// Kill a process and everything it started, ignoring errors.
        /// </summary>
        /// <param name="process">Process.</param>
        public static void KillTree(Process process)
        {
            try
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
            }
            catch (Exception)
            {
                // Already gone, or not ours to kill.
            }
        }

        #endregion

        #region Private-Methods

        private static bool ContainsWhitespace(string text)
        {
            foreach (char c in text)
            {
                if (Char.IsWhiteSpace(c)) return true;
            }

            return false;
        }

        #endregion
    }
}

namespace Xeno.Client.Hosting
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;

    /// <summary>
    /// How to start a Xeno executable: the file to run and any arguments that must precede the caller's arguments
    /// (for example the assembly path when the executable is the dotnet host).
    /// </summary>
    public class XenoExecutableLocation
    {
        /// <summary>
        /// Executable to run.
        /// </summary>
        public string Executable { get; set; } = String.Empty;

        /// <summary>
        /// Arguments placed before the caller's arguments.
        /// </summary>
        public List<string> LeadingArguments { get; set; } = new List<string>();

        /// <summary>
        /// Where the executable was found (environment, sibling, path, dotnet-tools, checkout, current-process).
        /// </summary>
        public string Source { get; set; } = String.Empty;

        /// <summary>
        /// Build process start information for the executable with additional arguments.
        /// </summary>
        /// <param name="arguments">Arguments after the leading arguments.</param>
        /// <returns>Start information with UseShellExecute disabled.</returns>
        public ProcessStartInfo CreateStartInfo(IEnumerable<string>? arguments)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo { FileName = Executable, UseShellExecute = false };
            foreach (string argument in LeadingArguments) startInfo.ArgumentList.Add(argument);
            if (arguments != null)
            {
                foreach (string argument in arguments) startInfo.ArgumentList.Add(argument);
            }

            return startInfo;
        }

        /// <summary>
        /// Command line for display and for shell scripts, with each part quoted for a POSIX shell.
        /// </summary>
        /// <param name="arguments">Arguments after the leading arguments.</param>
        /// <returns>Quoted command line.</returns>
        public string ToShellCommand(IEnumerable<string>? arguments)
        {
            List<string> parts = new List<string> { TerminalLaunchPlanner.QuotePosix(Executable) };
            foreach (string argument in LeadingArguments) parts.Add(TerminalLaunchPlanner.QuotePosix(argument));
            if (arguments != null)
            {
                foreach (string argument in arguments) parts.Add(TerminalLaunchPlanner.QuotePosix(argument));
            }

            return String.Join(" ", parts);
        }

        /// <summary>
        /// Command line for display and for cmd.exe, with each part quoted for Windows.
        /// </summary>
        /// <param name="arguments">Arguments after the leading arguments.</param>
        /// <returns>Quoted command line.</returns>
        public string ToWindowsCommand(IEnumerable<string>? arguments)
        {
            List<string> parts = new List<string> { TerminalLaunchPlanner.QuoteWindows(Executable) };
            foreach (string argument in LeadingArguments) parts.Add(TerminalLaunchPlanner.QuoteWindows(argument));
            if (arguments != null)
            {
                foreach (string argument in arguments) parts.Add(TerminalLaunchPlanner.QuoteWindows(argument));
            }

            return String.Join(" ", parts);
        }
    }
}

namespace Xeno.Client.Hosting
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;

    /// <summary>
    /// A planned process launch, kept as data so platform logic can be tested without starting processes.
    /// </summary>
    public class LaunchCommand
    {
        /// <summary>
        /// File to run.
        /// </summary>
        public string FileName { get; set; } = String.Empty;

        /// <summary>
        /// Arguments passed individually. Ignored when <see cref="RawArguments"/> is set.
        /// </summary>
        public List<string> Arguments { get; set; } = new List<string>();

        /// <summary>
        /// Raw argument string, used where quoting must be controlled exactly (cmd.exe start).
        /// </summary>
        public string? RawArguments { get; set; } = null;

        /// <summary>
        /// True to launch through the operating system shell (URLs and documents on Windows).
        /// </summary>
        public bool UseShellExecute { get; set; } = false;

        /// <summary>
        /// True to suppress a console window for the launcher process.
        /// </summary>
        public bool CreateNoWindow { get; set; } = false;

        /// <summary>
        /// Working directory, or null for the current directory.
        /// </summary>
        public string? WorkingDirectory { get; set; } = null;

        /// <summary>
        /// Script file that must be written before launching, or null.
        /// </summary>
        public string? ScriptPath { get; set; } = null;

        /// <summary>
        /// Contents of <see cref="ScriptPath"/>.
        /// </summary>
        public string? ScriptContent { get; set; } = null;

        /// <summary>
        /// Build process start information.
        /// </summary>
        /// <returns>Start information.</returns>
        public ProcessStartInfo ToStartInfo()
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = FileName,
                UseShellExecute = UseShellExecute,
                CreateNoWindow = CreateNoWindow
            };

            if (!String.IsNullOrWhiteSpace(WorkingDirectory)) startInfo.WorkingDirectory = WorkingDirectory;
            if (RawArguments != null)
            {
                startInfo.Arguments = RawArguments;
            }
            else
            {
                foreach (string argument in Arguments) startInfo.ArgumentList.Add(argument);
            }

            return startInfo;
        }
    }
}

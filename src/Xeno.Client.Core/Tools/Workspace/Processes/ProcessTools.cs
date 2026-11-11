namespace Xeno.Client.Tools.Workspace.Processes
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The process tools ported from Mux: run_process, and process_start, process_output, process_list, and
    /// process_stop over a <see cref="BackgroundProcessRegistry"/> that keeps background processes per conversation.
    /// run_process, process_start, and process_stop change things; process_output and process_list only read.
    /// </summary>
    public static class ProcessTools
    {
        #region Private-Members

        private const int MaxReturnedChars = 60000;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Create the tools.
        /// </summary>
        /// <param name="registry">Registry for background processes; the caller owns and disposes it.</param>
        /// <returns>Tools.</returns>
        /// <exception cref="ArgumentNullException">registry is null.</exception>
        public static IReadOnlyList<IWorkspaceTool> Create(BackgroundProcessRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            return new List<IWorkspaceTool>
            {
                new RunProcessTool(),
                new ProcessStartTool(registry),
                new ProcessOutputTool(registry),
                new ProcessListTool(registry),
                new ProcessStopTool(registry)
            };
        }

        #endregion

        #region Internal-Methods

        /// <summary>
        /// The newest part of long output, with a note saying how much is shown.
        /// </summary>
        /// <param name="text">Output.</param>
        /// <param name="maxToolOutputCharacters">Configured tool output limit.</param>
        /// <returns>Text.</returns>
        internal static string Clip(string text, int maxToolOutputCharacters)
        {
            int limit = Math.Max(500, Math.Min(MaxReturnedChars, maxToolOutputCharacters - 500));
            if (text.Length <= limit) return text;
            return "[xeno: showing the last " + limit + " of " + text.Length + " characters]\n" + text.Substring(text.Length - limit);
        }

        #endregion
    }
}

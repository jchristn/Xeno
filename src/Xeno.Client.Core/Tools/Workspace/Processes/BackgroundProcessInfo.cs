namespace Xeno.Client.Tools.Workspace.Processes
{
    using System;

    /// <summary>
    /// A point-in-time description of one background process: what it runs, where, and whether it is still alive.
    /// </summary>
    public sealed class BackgroundProcessInfo
    {
        /// <summary>
        /// Conversation that started the process.
        /// </summary>
        public string ConversationId { get; set; } = String.Empty;

        /// <summary>
        /// Short id used by the process tools, unique within the conversation (for example p1).
        /// </summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Optional friendly name given at start, or empty.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// The command and its arguments as one line.
        /// </summary>
        public string Command { get; set; } = String.Empty;

        /// <summary>
        /// Full working directory.
        /// </summary>
        public string WorkingDirectory { get; set; } = String.Empty;

        /// <summary>
        /// Operating-system process id, or 0 when unknown.
        /// </summary>
        public int ProcessId { get; set; } = 0;

        /// <summary>
        /// When the process started (UTC).
        /// </summary>
        public DateTime StartedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When the process exited (UTC), or null while it runs.
        /// </summary>
        public DateTime? ExitedUtc { get; set; } = null;

        /// <summary>
        /// Whether the process is still running.
        /// </summary>
        public bool Running { get; set; } = false;

        /// <summary>
        /// Exit code once the process has exited, or null while it runs.
        /// </summary>
        public int? ExitCode { get; set; } = null;

        /// <summary>
        /// Whether Xeno stopped the process (as opposed to it exiting on its own).
        /// </summary>
        public bool StoppedByUser { get; set; } = false;

        /// <summary>
        /// Total characters of output produced so far, including any dropped from the buffer.
        /// </summary>
        public long OutputChars { get; set; } = 0;

        /// <summary>
        /// Characters of output not yet returned by a read.
        /// </summary>
        public long UnreadChars { get; set; } = 0;
    }
}

namespace Xeno.Client.Tools.Workspace.Processes
{
    using System;
    using System.Diagnostics;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    /// <summary>
    /// The live state of one background process owned by a <see cref="BackgroundProcessRegistry"/>: the process, a
    /// bounded output buffer that keeps the newest text, and the read cursor. Thread safe.
    /// </summary>
    internal sealed class BackgroundProcessEntry
    {
        #region Private-Members

        private static readonly Regex _AnsiPattern = new Regex(@"\x1B(\[[0-9;?]*[ -/]*[@-~]|\][^\x07\x1B]*(\x07|\x1B\\)|[@-Z\\-_])", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private readonly object _Sync = new object();
        private readonly StringBuilder _Buffer = new StringBuilder();
        private readonly int _CapacityChars;
        private long _BufferStart = 0;
        private long _Total = 0;
        private long _ReadCursor = 0;
        private TaskCompletionSource<bool> _Signal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the entry.
        /// </summary>
        /// <param name="info">Descriptive information; the entry keeps and updates it.</param>
        /// <param name="process">The process.</param>
        /// <param name="capacityChars">Most characters of output kept.</param>
        public BackgroundProcessEntry(BackgroundProcessInfo info, Process process, int capacityChars)
        {
            Info = info;
            Process = process;
            _CapacityChars = Math.Max(1024, capacityChars);
        }

        #endregion

        #region Public-Members

        /// <summary>
        /// Descriptive information (changed under the entry's lock).
        /// </summary>
        public BackgroundProcessInfo Info { get; }

        /// <summary>
        /// The process.
        /// </summary>
        public Process Process { get; }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Append one line of output, dropping the oldest text when the buffer is full, and wake readers.
        /// </summary>
        /// <param name="line">Line without its newline. ANSI escape sequences are removed.</param>
        public void AppendLine(string line)
        {
            string clean = _AnsiPattern.Replace(line ?? String.Empty, String.Empty) + "\n";
            TaskCompletionSource<bool> signal;
            lock (_Sync)
            {
                _Buffer.Append(clean);
                _Total += clean.Length;
                if (_Buffer.Length > _CapacityChars)
                {
                    int drop = _Buffer.Length - _CapacityChars;
                    _Buffer.Remove(0, drop);
                    _BufferStart += drop;
                }

                Info.OutputChars = _Total;
                signal = _Signal;
                _Signal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            signal.TrySetResult(true);
        }

        /// <summary>
        /// Record that the process exited and wake readers.
        /// </summary>
        /// <param name="exitCode">Exit code.</param>
        /// <param name="exitedUtc">When it exited.</param>
        /// <returns>True when this call changed the state.</returns>
        public bool MarkExited(int exitCode, DateTime exitedUtc)
        {
            TaskCompletionSource<bool> signal;
            lock (_Sync)
            {
                if (!Info.Running) return false;
                Info.Running = false;
                Info.ExitCode = exitCode;
                Info.ExitedUtc = exitedUtc;
                signal = _Signal;
                _Signal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            signal.TrySetResult(true);
            return true;
        }

        /// <summary>
        /// The unread text without consuming it.
        /// </summary>
        /// <returns>Unread text still in the buffer.</returns>
        public string PeekUnread()
        {
            lock (_Sync)
            {
                return PeekUnreadNoLock().Text;
            }
        }

        /// <summary>
        /// The unread text, advancing the read cursor to the end.
        /// </summary>
        /// <returns>Unread text and how many unread characters were lost to the buffer limit.</returns>
        public BackgroundProcessOutput ReadUnread()
        {
            lock (_Sync)
            {
                BackgroundProcessOutput output = PeekUnreadNoLock();
                _ReadCursor = _Total;
                Info.UnreadChars = 0;
                output.Id = Info.Id;
                output.Running = Info.Running;
                output.ExitCode = Info.ExitCode;
                return output;
            }
        }

        /// <summary>
        /// A task that completes the next time output arrives or the process exits.
        /// </summary>
        /// <returns>Signal task.</returns>
        public Task WaitForChangeAsync()
        {
            lock (_Sync)
            {
                return _Signal.Task;
            }
        }

        /// <summary>
        /// A copy of the descriptive information with the unread count filled in.
        /// </summary>
        /// <returns>Snapshot.</returns>
        public BackgroundProcessInfo Snapshot()
        {
            lock (_Sync)
            {
                Info.UnreadChars = _Total - _ReadCursor;
                return new BackgroundProcessInfo
                {
                    ConversationId = Info.ConversationId,
                    Id = Info.Id,
                    Name = Info.Name,
                    Command = Info.Command,
                    WorkingDirectory = Info.WorkingDirectory,
                    ProcessId = Info.ProcessId,
                    StartedUtc = Info.StartedUtc,
                    ExitedUtc = Info.ExitedUtc,
                    Running = Info.Running,
                    ExitCode = Info.ExitCode,
                    StoppedByUser = Info.StoppedByUser,
                    OutputChars = Info.OutputChars,
                    UnreadChars = Info.UnreadChars
                };
            }
        }

        /// <summary>
        /// Whether the process is still running.
        /// </summary>
        /// <returns>True while it runs.</returns>
        public bool IsRunning()
        {
            lock (_Sync)
            {
                return Info.Running;
            }
        }

        /// <summary>
        /// Mark the process as stopped by Xeno.
        /// </summary>
        public void MarkStoppedByUser()
        {
            lock (_Sync)
            {
                Info.StoppedByUser = true;
            }
        }

        #endregion

        #region Private-Methods

        private BackgroundProcessOutput PeekUnreadNoLock()
        {
            long from = Math.Max(_ReadCursor, _BufferStart);
            int offset = (int)(from - _BufferStart);
            return new BackgroundProcessOutput
            {
                Text = _Buffer.ToString(offset, _Buffer.Length - offset),
                DroppedChars = from - _ReadCursor
            };
        }

        #endregion
    }
}

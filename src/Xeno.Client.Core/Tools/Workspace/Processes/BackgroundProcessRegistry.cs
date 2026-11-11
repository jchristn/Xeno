namespace Xeno.Client.Tools.Workspace.Processes
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Owns the background processes started by chat, kept per conversation: dev servers, <c>docker compose up</c>,
    /// watch-mode test runners, and anything else that does not fit run_process with a timeout. Ported from Mux. Each
    /// process's stdout and stderr go to a bounded buffer that keeps the newest text, and reads return only what is new
    /// since the previous read. The number of running processes per conversation is capped, exited processes beyond a
    /// limit are forgotten, and <see cref="Dispose"/> kills every process tree so nothing outlives the host. Output is
    /// never logged. Thread safe.
    /// </summary>
    public sealed class BackgroundProcessRegistry : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Default number of processes one conversation may run at once.
        /// </summary>
        public const int DefaultMaxConcurrent = 8;

        /// <summary>
        /// Highest allowed concurrency setting.
        /// </summary>
        public const int MaxConcurrentLimit = 64;

        /// <summary>
        /// Most processes (running and exited) kept per conversation; the oldest exited ones are forgotten first.
        /// </summary>
        public const int MaxKeptPerConversation = 32;

        /// <summary>
        /// Default output kept per process (1 MB of text).
        /// </summary>
        public const int DefaultOutputCapacity = 1048576;

        /// <summary>
        /// Smallest allowed output buffer.
        /// </summary>
        public const int MinOutputCapacity = 16384;

        /// <summary>
        /// Largest allowed output buffer.
        /// </summary>
        public const int MaxOutputCapacity = 16777216;

        /// <summary>
        /// Longest a single read may wait, in milliseconds (1 hour). Timing out never stops the process.
        /// </summary>
        public const int MaxWaitMs = 3600000;

        /// <summary>
        /// Most processes one conversation may run at once.
        /// </summary>
        public int MaxConcurrent
        {
            get { return _MaxConcurrent; }
        }

        /// <summary>
        /// Output kept per process, in characters.
        /// </summary>
        public int OutputCapacityChars
        {
            get { return _OutputCapacityChars; }
        }

        #endregion

        #region Private-Members

        private readonly object _Sync = new object();
        private readonly Dictionary<string, ConversationProcesses> _Conversations = new Dictionary<string, ConversationProcesses>(StringComparer.Ordinal);
        private readonly int _MaxConcurrent;
        private readonly int _OutputCapacityChars;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the registry.
        /// </summary>
        /// <param name="maxConcurrent">Most processes one conversation may run at once (clamped to 1..<see cref="MaxConcurrentLimit"/>).</param>
        /// <param name="outputCapacityChars">Output kept per process, in characters (clamped to <see cref="MinOutputCapacity"/>..<see cref="MaxOutputCapacity"/>).</param>
        public BackgroundProcessRegistry(int maxConcurrent = DefaultMaxConcurrent, int outputCapacityChars = DefaultOutputCapacity)
        {
            _MaxConcurrent = Math.Clamp(maxConcurrent, 1, MaxConcurrentLimit);
            _OutputCapacityChars = Math.Clamp(outputCapacityChars, MinOutputCapacity, MaxOutputCapacity);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Start a process in the background. The command is a program and args its arguments, with no shell, unless
        /// shell is true (then the joined command line runs through <c>/bin/sh -c</c> or <c>cmd.exe /c</c>).
        /// </summary>
        /// <param name="conversationId">Conversation that owns the process.</param>
        /// <param name="command">Program, or command line with shell.</param>
        /// <param name="args">Arguments, or null.</param>
        /// <param name="shell">Run through the platform shell.</param>
        /// <param name="workingDirectory">Full, existing working directory (already confined by the caller).</param>
        /// <param name="name">Optional friendly name.</param>
        /// <returns>Snapshot of the started process.</returns>
        /// <exception cref="ArgumentException">The command is blank or the directory does not exist.</exception>
        /// <exception cref="InvalidOperationException">The conversation's limit is reached, the program could not start, or the registry is disposed.</exception>
        public BackgroundProcessInfo Start(string conversationId, string? command, IReadOnlyList<string>? args, bool shell, string workingDirectory, string? name = null)
        {
            string key = conversationId ?? String.Empty;
            if (String.IsNullOrWhiteSpace(workingDirectory) || !Directory.Exists(workingDirectory))
            {
                throw new ArgumentException("The working directory '" + workingDirectory + "' does not exist.");
            }

            ProcessStartInfo startInfo = ProcessCommandLine.Create(command, args, shell, workingDirectory);
            BackgroundProcessEntry entry;
            lock (_Sync)
            {
                if (_Disposed) throw new InvalidOperationException("The background process registry has been disposed.");
                if (!_Conversations.TryGetValue(key, out ConversationProcesses? processes))
                {
                    processes = new ConversationProcesses();
                    _Conversations[key] = processes;
                }

                int running = processes.Entries.FindAll(item => item.IsRunning()).Count;
                if (running >= _MaxConcurrent)
                {
                    throw new InvalidOperationException("Already running " + running + " background processes in this conversation (the limit is " + _MaxConcurrent + "). Stop one with process_stop first.");
                }

                while (processes.Entries.Count >= MaxKeptPerConversation)
                {
                    BackgroundProcessEntry? oldest = processes.Entries.Find(item => !item.IsRunning());
                    if (oldest == null) break;
                    processes.Entries.Remove(oldest);
                    oldest.Process.Dispose();
                }

                Process process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
                BackgroundProcessInfo info = new BackgroundProcessInfo
                {
                    ConversationId = key,
                    Id = "p" + processes.NextNumber.ToString(CultureInfo.InvariantCulture),
                    Name = (name ?? String.Empty).Trim(),
                    Command = ProcessCommandLine.Display(command ?? String.Empty, args),
                    WorkingDirectory = workingDirectory,
                    StartedUtc = DateTime.UtcNow,
                    Running = true
                };
                BackgroundProcessEntry created = new BackgroundProcessEntry(info, process, _OutputCapacityChars);
                process.OutputDataReceived += (object sender, DataReceivedEventArgs e) => { if (e.Data != null) created.AppendLine(e.Data); };
                process.ErrorDataReceived += (object sender, DataReceivedEventArgs e) => { if (e.Data != null) created.AppendLine(e.Data); };
                process.Exited += (object? sender, EventArgs e) => OnExited(created);
                try
                {
                    process.Start();
                }
                catch (Win32Exception ex)
                {
                    process.Dispose();
                    throw new InvalidOperationException("Could not start '" + startInfo.FileName + "': " + ex.Message);
                }

                processes.NextNumber++;
                info.ProcessId = SafeProcessId(process);
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                try { process.StandardInput.Close(); } catch (Exception) { }
                processes.Entries.Add(created);
                entry = created;
            }

            return entry.Snapshot();
        }

        /// <summary>
        /// Snapshots of a conversation's processes (running and exited), oldest first.
        /// </summary>
        /// <param name="conversationId">Conversation.</param>
        /// <returns>Processes.</returns>
        public IReadOnlyList<BackgroundProcessInfo> List(string conversationId)
        {
            lock (_Sync)
            {
                if (!_Conversations.TryGetValue(conversationId ?? String.Empty, out ConversationProcesses? processes)) return new List<BackgroundProcessInfo>();
                return processes.Entries.ConvertAll(item => item.Snapshot());
            }
        }

        /// <summary>
        /// Snapshot of one process.
        /// </summary>
        /// <param name="conversationId">Conversation.</param>
        /// <param name="id">Process id (case-insensitive).</param>
        /// <returns>Snapshot, or null when the conversation has no process with that id.</returns>
        public BackgroundProcessInfo? Get(string conversationId, string id)
        {
            return Find(conversationId, id)?.Snapshot();
        }

        /// <summary>
        /// Number of running processes across all conversations.
        /// </summary>
        /// <returns>Count.</returns>
        public int CountRunning()
        {
            lock (_Sync)
            {
                int count = 0;
                foreach (ConversationProcesses processes in _Conversations.Values)
                {
                    count += processes.Entries.FindAll(item => item.IsRunning()).Count;
                }

                return count;
            }
        }

        /// <summary>
        /// Read the output produced since the previous read. With a pattern, waits until the pattern appears in the
        /// unread output, the process exits, or the timeout passes; without one, waits up to the timeout only when
        /// there is nothing new yet.
        /// </summary>
        /// <param name="conversationId">Conversation.</param>
        /// <param name="id">Process id.</param>
        /// <param name="waitFor">Regular expression to wait for, or null.</param>
        /// <param name="timeoutMs">How long to wait, in milliseconds (0 to <see cref="MaxWaitMs"/>).</param>
        /// <param name="token">Cancels the wait.</param>
        /// <returns>Output and process state.</returns>
        /// <exception cref="KeyNotFoundException">No process has the id.</exception>
        /// <exception cref="ArgumentException">The pattern is not a valid regular expression.</exception>
        public async Task<BackgroundProcessOutput> ReadAsync(string conversationId, string id, string? waitFor, int timeoutMs, CancellationToken token)
        {
            BackgroundProcessEntry entry = Find(conversationId, id) ?? throw new KeyNotFoundException("No background process has the id '" + id + "'. Use process_list to see the ids.");
            Regex? pattern = null;
            if (!String.IsNullOrEmpty(waitFor))
            {
                try
                {
                    pattern = new Regex(waitFor, RegexOptions.CultureInvariant | RegexOptions.Multiline, TimeSpan.FromSeconds(2));
                }
                catch (ArgumentException ex)
                {
                    throw new ArgumentException("wait_for is not a valid regular expression: " + ex.Message);
                }
            }

            int wait = Math.Clamp(timeoutMs, 0, MaxWaitMs);
            Stopwatch watch = Stopwatch.StartNew();
            bool timedOut = false;
            Match? match = null;
            while (true)
            {
                Task changed = entry.WaitForChangeAsync();
                string unread = entry.PeekUnread();
                bool running = entry.IsRunning();
                if (pattern != null)
                {
                    match = SafeMatch(pattern, unread);
                    if (match != null || !running) break;
                }
                else if (unread.Length > 0 || !running || wait == 0)
                {
                    break;
                }

                TimeSpan remaining = TimeSpan.FromMilliseconds(wait) - watch.Elapsed;
                if (remaining <= TimeSpan.Zero)
                {
                    timedOut = wait > 0 || pattern != null;
                    break;
                }

                using (CancellationTokenSource delay = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    Task timer = Task.Delay(remaining, delay.Token);
                    await Task.WhenAny(changed, timer).ConfigureAwait(false);
                    delay.Cancel();
                }

                token.ThrowIfCancellationRequested();
            }

            BackgroundProcessOutput output = entry.ReadUnread();
            output.Matched = pattern == null ? null : match != null;
            output.MatchText = match?.Value;
            output.TimedOut = timedOut && match == null;
            return output;
        }

        /// <summary>
        /// Stop a process and its children, waiting briefly for it to exit, and return its unread output.
        /// </summary>
        /// <param name="conversationId">Conversation.</param>
        /// <param name="id">Process id.</param>
        /// <param name="token">Cancels the wait for exit.</param>
        /// <returns>Final output and state.</returns>
        /// <exception cref="KeyNotFoundException">No process has the id.</exception>
        public async Task<BackgroundProcessOutput> StopAsync(string conversationId, string id, CancellationToken token)
        {
            BackgroundProcessEntry entry = Find(conversationId, id) ?? throw new KeyNotFoundException("No background process has the id '" + id + "'. Use process_list to see the ids.");
            if (entry.IsRunning())
            {
                entry.MarkStoppedByUser();
                ProcessCommandLine.KillTree(entry.Process);
                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                using (CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, token))
                {
                    try
                    {
                        await entry.Process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        token.ThrowIfCancellationRequested();
                    }
                }

                OnExited(entry);
            }

            return entry.ReadUnread();
        }

        /// <summary>
        /// Stop every running process of a conversation.
        /// </summary>
        /// <param name="conversationId">Conversation.</param>
        /// <returns>Number of processes stopped.</returns>
        public int StopAll(string conversationId)
        {
            List<BackgroundProcessEntry> running;
            lock (_Sync)
            {
                if (!_Conversations.TryGetValue(conversationId ?? String.Empty, out ConversationProcesses? processes)) return 0;
                running = processes.Entries.FindAll(item => item.IsRunning());
            }

            StopEntries(running);
            return running.Count;
        }

        /// <summary>
        /// Stop a conversation's processes and forget them, for example when the conversation is deleted.
        /// </summary>
        /// <param name="conversationId">Conversation.</param>
        /// <returns>Number of processes stopped.</returns>
        public int RemoveConversation(string conversationId)
        {
            int stopped = StopAll(conversationId);
            lock (_Sync)
            {
                if (_Conversations.TryGetValue(conversationId ?? String.Empty, out ConversationProcesses? processes))
                {
                    foreach (BackgroundProcessEntry entry in processes.Entries) entry.Process.Dispose();
                    _Conversations.Remove(conversationId ?? String.Empty);
                }
            }

            return stopped;
        }

        /// <summary>
        /// Kill every process tree of every conversation and refuse further starts.
        /// </summary>
        public void Dispose()
        {
            List<BackgroundProcessEntry> all = new List<BackgroundProcessEntry>();
            lock (_Sync)
            {
                if (_Disposed) return;
                _Disposed = true;
                foreach (ConversationProcesses processes in _Conversations.Values) all.AddRange(processes.Entries);
            }

            StopEntries(all.FindAll(item => item.IsRunning()));
            lock (_Sync)
            {
                foreach (BackgroundProcessEntry entry in all) entry.Process.Dispose();
                _Conversations.Clear();
            }
        }

        #endregion

        #region Private-Methods

        private BackgroundProcessEntry? Find(string conversationId, string id)
        {
            string key = (id ?? String.Empty).Trim();
            lock (_Sync)
            {
                if (!_Conversations.TryGetValue(conversationId ?? String.Empty, out ConversationProcesses? processes)) return null;
                return processes.Entries.Find(item => String.Equals(item.Info.Id, key, StringComparison.OrdinalIgnoreCase));
            }
        }

        private void StopEntries(List<BackgroundProcessEntry> entries)
        {
            foreach (BackgroundProcessEntry entry in entries)
            {
                entry.MarkStoppedByUser();
                ProcessCommandLine.KillTree(entry.Process);
            }

            foreach (BackgroundProcessEntry entry in entries)
            {
                try { entry.Process.WaitForExit(3000); } catch (Exception) { }
                OnExited(entry);
            }
        }

        private static void OnExited(BackgroundProcessEntry entry)
        {
            Process process = entry.Process;
            int exitCode = -1;
            bool exited;
            try
            {
                exited = process.HasExited;
                if (exited)
                {
                    // Drain the redirected streams so the last lines arrive before the exit is reported.
                    process.WaitForExit(5000);
                    exitCode = process.ExitCode;
                }
            }
            catch (Exception)
            {
                exited = true;
            }

            if (exited) entry.MarkExited(exitCode, DateTime.UtcNow);
        }

        private static int SafeProcessId(Process process)
        {
            try { return process.Id; } catch (Exception) { return 0; }
        }

        private static Match? SafeMatch(Regex pattern, string text)
        {
            try
            {
                Match match = pattern.Match(text);
                return match.Success ? match : null;
            }
            catch (RegexMatchTimeoutException)
            {
                return null;
            }
        }

        #endregion

        #region Private-Classes

        private sealed class ConversationProcesses
        {
            public List<BackgroundProcessEntry> Entries { get; } = new List<BackgroundProcessEntry>();

            public int NextNumber { get; set; } = 1;
        }

        #endregion
    }
}

namespace Xeno.Client.Tools.Workspace.Processes
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Chat.Wire;

    /// <summary>
    /// process_start, ported from Mux: starts a long-running process in the background for this conversation and
    /// returns its id, optionally waiting for a line that shows it is ready.
    /// </summary>
    public sealed class ProcessStartTool : IWorkspaceTool
    {
        #region Public-Members

        /// <summary>
        /// Tool name.
        /// </summary>
        public const string ToolName = "process_start";

        /// <inheritdoc />
        public string Name
        {
            get { return ToolName; }
        }

        /// <inheritdoc />
        public string Description
        {
            get
            {
                return "Start a long-running command in the background (a dev server, docker compose up, a watch-mode test runner) and return its id at once. "
                    + "The command is a program and args are its arguments, run without a shell; set shell to true to run a command line through " + ProcessCommandLine.ShellInvocation + ". "
                    + "Use run_process instead for commands that finish. Optionally wait for a line that shows it is ready.";
            }
        }

        /// <inheritdoc />
        public ChatToolSchemaDto Schema
        {
            get
            {
                return WorkspaceSchema.Object(
                    WorkspaceSchema.Text("command", "The program to run, or with shell true the command line to run."),
                    WorkspaceSchema.Array("args", "Optional array of command arguments, passed to the program as they are.", new ChatToolPropertyDto { Type = "string" }),
                    WorkspaceSchema.Bool("shell", "Run the command (joined with args) through " + ProcessCommandLine.ShellInvocation + ". Defaults to false."),
                    WorkspaceSchema.Text("working_directory", "Working directory, inside the conversation's working directory. Defaults to the conversation's working directory."),
                    WorkspaceSchema.Text("name", "Optional short label, for example 'web' or 'api'."),
                    WorkspaceSchema.Text("wait_for", "Optional regular expression; wait until the output matches it (for example 'ready|listening on|Local:')."),
                    WorkspaceSchema.Integer("timeout_ms", "How long to wait for wait_for, in milliseconds (default 30000, at most 3600000; a wait that times out never stops the process)."),
                    "command");
            }
        }

        /// <inheritdoc />
        public bool Mutating
        {
            get { return true; }
        }

        #endregion

        #region Private-Members

        private readonly BackgroundProcessRegistry _Registry;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the tool.
        /// </summary>
        /// <param name="registry">Process registry.</param>
        /// <exception cref="ArgumentNullException">registry is null.</exception>
        public ProcessStartTool(BackgroundProcessRegistry registry)
        {
            _Registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<object> InvokeAsync(string arguments, WorkspaceToolContext context, CancellationToken token)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            ProcessStartArguments args = WorkspaceJson.Read<ProcessStartArguments>(arguments);
            if (String.IsNullOrWhiteSpace(args.Command)) throw new ArgumentException("command is required.");
            string directory = WorkspacePath.Resolve(args.WorkingDirectory, context.WorkingDirectory);
            if (!Directory.Exists(directory)) throw new ArgumentException("The working directory '" + args.WorkingDirectory + "' does not exist.");
            BackgroundProcessInfo started = _Registry.Start(context.ConversationId, args.Command, args.Args, args.Shell, directory, args.Name);
            ProcessStartResult result = new ProcessStartResult
            {
                Id = started.Id,
                Pid = started.ProcessId,
                Command = started.Command,
                WorkingDirectory = started.WorkingDirectory
            };

            if (String.IsNullOrEmpty(args.WaitFor))
            {
                result.Next = "Read its output with process_output (id " + started.Id + "); stop it with process_stop.";
                return result;
            }

            BackgroundProcessOutput output = await _Registry.ReadAsync(context.ConversationId, started.Id, args.WaitFor, args.TimeoutMs ?? 30000, token).ConfigureAwait(false);
            result.Running = output.Running;
            result.ExitCode = output.ExitCode;
            result.Ready = output.Matched == true;
            result.Match = output.MatchText;
            result.TimedOut = output.TimedOut;
            result.Output = ProcessTools.Clip(output.Text, context.Settings.MaxToolOutputCharacters);
            result.DroppedChars = output.DroppedChars;
            return result;
        }

        #endregion
    }
}

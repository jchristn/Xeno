namespace Xeno.Client.Tools.Workspace.Processes
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Chat.Wire;

    /// <summary>
    /// process_output, ported from Mux: reads the output a background process produced since the last read.
    /// </summary>
    public sealed class ProcessOutputTool : IWorkspaceTool
    {
        #region Public-Members

        /// <summary>
        /// Tool name.
        /// </summary>
        public const string ToolName = "process_output";

        /// <inheritdoc />
        public string Name
        {
            get { return ToolName; }
        }

        /// <inheritdoc />
        public string Description
        {
            get { return "Read the output a background process produced since the last read, with its running state and exit code. Optionally wait until the output matches a regular expression."; }
        }

        /// <inheritdoc />
        public ChatToolSchemaDto Schema
        {
            get
            {
                return WorkspaceSchema.Object(
                    WorkspaceSchema.Text("id", "The process id from process_start, for example p1."),
                    WorkspaceSchema.Text("wait_for", "Optional regular expression to wait for in the new output."),
                    WorkspaceSchema.Integer("timeout_ms", "How long to wait, in milliseconds (default 0 without wait_for, 30000 with it; at most 3600000)."),
                    "id");
            }
        }

        /// <inheritdoc />
        public bool Mutating
        {
            get { return false; }
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
        public ProcessOutputTool(BackgroundProcessRegistry registry)
        {
            _Registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<object> InvokeAsync(string arguments, WorkspaceToolContext context, CancellationToken token)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            ProcessOutputArguments args = WorkspaceJson.Read<ProcessOutputArguments>(arguments);
            if (String.IsNullOrWhiteSpace(args.Id)) throw new ArgumentException("id is required.");
            int timeout = args.TimeoutMs ?? (String.IsNullOrEmpty(args.WaitFor) ? 0 : 30000);
            BackgroundProcessOutput output = await _Registry.ReadAsync(context.ConversationId, args.Id, args.WaitFor, timeout, token).ConfigureAwait(false);
            return new ProcessOutputResult
            {
                Id = output.Id,
                Running = output.Running,
                ExitCode = output.ExitCode,
                Matched = output.Matched,
                Match = output.MatchText,
                TimedOut = output.TimedOut,
                Output = ProcessTools.Clip(output.Text, context.Settings.MaxToolOutputCharacters),
                DroppedChars = output.DroppedChars
            };
        }

        #endregion
    }
}

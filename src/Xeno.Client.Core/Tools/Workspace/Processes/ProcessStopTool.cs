namespace Xeno.Client.Tools.Workspace.Processes
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Chat.Wire;

    /// <summary>
    /// process_stop, ported from Mux: stops a background process and its child processes, or all of this
    /// conversation's processes.
    /// </summary>
    public sealed class ProcessStopTool : IWorkspaceTool
    {
        #region Public-Members

        /// <summary>
        /// Tool name.
        /// </summary>
        public const string ToolName = "process_stop";

        /// <inheritdoc />
        public string Name
        {
            get { return ToolName; }
        }

        /// <inheritdoc />
        public string Description
        {
            get { return "Stop a background process and its child processes, and return its last unread output. Pass id 'all' to stop every one."; }
        }

        /// <inheritdoc />
        public ChatToolSchemaDto Schema
        {
            get
            {
                return WorkspaceSchema.Object(
                    WorkspaceSchema.Text("id", "The process id, or 'all'."),
                    "id");
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
        public ProcessStopTool(BackgroundProcessRegistry registry)
        {
            _Registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<object> InvokeAsync(string arguments, WorkspaceToolContext context, CancellationToken token)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            ProcessStopArguments args = WorkspaceJson.Read<ProcessStopArguments>(arguments);
            if (String.IsNullOrWhiteSpace(args.Id)) throw new ArgumentException("id is required.");
            if (String.Equals(args.Id.Trim(), "all", StringComparison.OrdinalIgnoreCase))
            {
                return new ProcessStopResult { Stopped = _Registry.StopAll(context.ConversationId) };
            }

            BackgroundProcessInfo? before = _Registry.Get(context.ConversationId, args.Id);
            BackgroundProcessOutput output = await _Registry.StopAsync(context.ConversationId, args.Id, token).ConfigureAwait(false);
            return new ProcessStopResult
            {
                Id = output.Id,
                WasRunning = before?.Running ?? false,
                Running = output.Running,
                ExitCode = output.ExitCode,
                Output = ProcessTools.Clip(output.Text, context.Settings.MaxToolOutputCharacters),
                DroppedChars = output.DroppedChars
            };
        }

        #endregion
    }
}

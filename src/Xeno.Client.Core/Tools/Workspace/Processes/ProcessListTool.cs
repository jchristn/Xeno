namespace Xeno.Client.Tools.Workspace.Processes
{
    using System;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Chat.Wire;

    /// <summary>
    /// process_list, ported from Mux: lists this conversation's background processes.
    /// </summary>
    public sealed class ProcessListTool : IWorkspaceTool
    {
        #region Public-Members

        /// <summary>
        /// Tool name.
        /// </summary>
        public const string ToolName = "process_list";

        /// <inheritdoc />
        public string Name
        {
            get { return ToolName; }
        }

        /// <inheritdoc />
        public string Description
        {
            get { return "List this conversation's background processes with their ids, commands, state, and unread output."; }
        }

        /// <inheritdoc />
        public ChatToolSchemaDto Schema
        {
            get { return WorkspaceSchema.Object(); }
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
        public ProcessListTool(BackgroundProcessRegistry registry)
        {
            _Registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public Task<object> InvokeAsync(string arguments, WorkspaceToolContext context, CancellationToken token)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            ProcessListResult result = new ProcessListResult();
            foreach (BackgroundProcessInfo info in _Registry.List(context.ConversationId))
            {
                result.Processes.Add(new ProcessListItem
                {
                    Id = info.Id,
                    Name = info.Name,
                    Command = info.Command,
                    WorkingDirectory = info.WorkingDirectory,
                    Pid = info.ProcessId,
                    Running = info.Running,
                    ExitCode = info.ExitCode,
                    StoppedByUser = info.StoppedByUser,
                    StartedUtc = info.StartedUtc.ToString("o", CultureInfo.InvariantCulture),
                    UnreadChars = info.UnreadChars
                });
            }

            return Task.FromResult<object>(result);
        }

        #endregion
    }
}

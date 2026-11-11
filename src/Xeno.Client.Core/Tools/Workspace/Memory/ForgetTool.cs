namespace Xeno.Client.Tools.Workspace.Memory
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Chat.Wire;

    /// <summary>
    /// forget: deletes a memory. Mutating, as in Mux, so chat asks before it runs.
    /// </summary>
    public sealed class ForgetTool : IWorkspaceTool
    {
        #region Public-Members

        /// <summary>
        /// Tool name.
        /// </summary>
        public const string ToolName = "forget";

        /// <inheritdoc />
        public string Name
        {
            get { return ToolName; }
        }

        /// <inheritdoc />
        public string Description
        {
            get { return "Delete a memory that is wrong or no longer true."; }
        }

        /// <inheritdoc />
        public ChatToolSchemaDto Schema
        {
            get
            {
                return WorkspaceSchema.Object(
                    WorkspaceSchema.Text("name", "The memory's name."),
                    MemoryToolSupport.ScopeProperty(),
                    "name");
            }
        }

        /// <inheritdoc />
        public bool Mutating
        {
            get { return true; }
        }

        #endregion

        #region Private-Members

        private readonly MemoryStore _Store;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the tool.
        /// </summary>
        /// <param name="store">Memory store.</param>
        /// <exception cref="ArgumentNullException">store is null.</exception>
        public ForgetTool(MemoryStore store)
        {
            _Store = store ?? throw new ArgumentNullException(nameof(store));
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public Task<object> InvokeAsync(string arguments, WorkspaceToolContext context, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(context);
            token.ThrowIfCancellationRequested();
            return Task.FromResult(MemoryToolSupport.Run(delegate
            {
                ForgetArguments args = WorkspaceJson.Read<ForgetArguments>(arguments);
                string name = MemoryToolSupport.Text(args.Name) ?? throw new ArgumentException("name is required.");
                MemoryEntry? deleted = _Store.Delete(name, MemoryToolSupport.Scope(args.Scope), context.WorkingDirectory);
                if (deleted == null) return new MemoryErrorResult { Error = "not_found", Message = "No memory named '" + name + "'. Use recall to list them." };
                return new MemoryDeletedResult { Deleted = true, Name = deleted.Slug, Scope = MemoryStore.ScopeName(deleted.Scope) };
            }));
        }

        #endregion
    }
}

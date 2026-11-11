namespace Xeno.Client.Tools.Workspace.Memory
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Chat.Wire;

    /// <summary>
    /// remember: saves a durable fact to persistent memory, or updates the memory with the same name. Mutating, as in
    /// Mux, so chat asks before it runs.
    /// </summary>
    public sealed class RememberTool : IWorkspaceTool
    {
        #region Public-Members

        /// <summary>
        /// Tool name.
        /// </summary>
        public const string ToolName = "remember";

        /// <inheritdoc />
        public string Name
        {
            get { return ToolName; }
        }

        /// <inheritdoc />
        public string Description
        {
            get { return "Save a durable fact, preference, or decision to persistent memory so future sessions know it. Saving with an existing name updates that memory. Never store secrets."; }
        }

        /// <inheritdoc />
        public ChatToolSchemaDto Schema
        {
            get
            {
                return WorkspaceSchema.Object(
                    WorkspaceSchema.Text("name", "Short name, for example 'test-command' or 'prefers-tabs'."),
                    WorkspaceSchema.Text("description", "One line shown in the memory index."),
                    WorkspaceSchema.Text("content", "The full fact, with any reason or context. Defaults to the description."),
                    MemoryToolSupport.ScopeProperty(),
                    "name",
                    "description");
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
        public RememberTool(MemoryStore store)
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
                RememberArguments args = WorkspaceJson.Read<RememberArguments>(arguments);
                string name = MemoryToolSupport.Text(args.Name) ?? throw new ArgumentException("name is required.");
                MemoryScopeEnum scope = MemoryToolSupport.Scope(args.Scope) ?? MemoryScopeEnum.Project;
                MemoryEntry entry = _Store.Save(name, MemoryToolSupport.Text(args.Description), MemoryToolSupport.Text(args.Content), scope, context.WorkingDirectory, out bool created);
                return new MemorySavedResult { Saved = true, Created = created, Name = entry.Slug, Scope = MemoryStore.ScopeName(entry.Scope), Description = entry.Description };
            }));
        }

        #endregion
    }
}

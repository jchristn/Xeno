namespace Xeno.Client.Tools.Workspace.Memory
{
    using System;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Chat.Wire;

    /// <summary>
    /// recall: reads one memory in full by name, or searches the project and global memories. Read-only.
    /// </summary>
    public sealed class RecallTool : IWorkspaceTool
    {
        #region Public-Members

        /// <summary>
        /// Tool name.
        /// </summary>
        public const string ToolName = "recall";

        /// <inheritdoc />
        public string Name
        {
            get { return ToolName; }
        }

        /// <inheritdoc />
        public string Description
        {
            get { return "Read one memory in full by name, or search memories by text (every word must match). With neither, lists all memories."; }
        }

        /// <inheritdoc />
        public ChatToolSchemaDto Schema
        {
            get
            {
                return WorkspaceSchema.Object(
                    WorkspaceSchema.Text("name", "A memory name to read in full."),
                    WorkspaceSchema.Text("query", "Words to search for in names, descriptions, and content."));
            }
        }

        /// <inheritdoc />
        public bool Mutating
        {
            get { return false; }
        }

        #endregion

        #region Private-Members

        private const int MaxMatchContentLength = 500;

        private readonly MemoryStore _Store;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the tool.
        /// </summary>
        /// <param name="store">Memory store.</param>
        /// <exception cref="ArgumentNullException">store is null.</exception>
        public RecallTool(MemoryStore store)
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
                RecallArguments args = WorkspaceJson.Read<RecallArguments>(arguments);
                string? name = MemoryToolSupport.Text(args.Name);
                if (name != null)
                {
                    MemoryEntry? entry = _Store.Get(name, null, context.WorkingDirectory);
                    if (entry == null) return new MemoryErrorResult { Error = "not_found", Message = "No memory named '" + name + "'." };
                    return new MemoryReadResult
                    {
                        Name = entry.Slug,
                        Scope = MemoryStore.ScopeName(entry.Scope),
                        Description = entry.Description,
                        Content = entry.Content,
                        UpdatedUtc = entry.UpdatedUtc.ToString("o", CultureInfo.InvariantCulture)
                    };
                }

                MemorySearchResult result = new MemorySearchResult();
                foreach (MemoryEntry entry in _Store.Search(MemoryToolSupport.Text(args.Query), context.WorkingDirectory))
                {
                    result.Memories.Add(new MemoryMatch { Name = entry.Slug, Scope = MemoryStore.ScopeName(entry.Scope), Description = entry.Description, Content = Clip(entry.Content) });
                }

                result.Count = result.Memories.Count;
                return result;
            }));
        }

        #endregion

        #region Private-Methods

        private static string Clip(string text)
        {
            return text.Length <= MaxMatchContentLength ? text : text.Substring(0, MaxMatchContentLength) + "...";
        }

        #endregion
    }
}

namespace Xeno.Client.Tools.Workspace.Memory
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Builds the system prompt section that lists remembered facts for a project (and the global ones) as an index of
    /// names and descriptions, newest first, within a byte budget. Entries that do not fit are left out with a note, so
    /// the model knows to use recall.
    /// </summary>
    public static class MemoryPromptBuilder
    {
        #region Public-Members

        /// <summary>
        /// Default byte budget for the index lines (Mux's memoryMaxBytes default).
        /// </summary>
        public const int DefaultMaxBytes = 16384;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Build the memory section.
        /// </summary>
        /// <param name="store">Memory store.</param>
        /// <param name="workingDirectory">Conversation's working directory, which selects the project.</param>
        /// <param name="maxBytes">Most UTF-8 bytes the index lines may use; 0 omits every entry.</param>
        /// <returns>Section, starting with a blank line.</returns>
        /// <exception cref="ArgumentNullException">store is null.</exception>
        public static string Build(MemoryStore store, string? workingDirectory, int maxBytes)
        {
            ArgumentNullException.ThrowIfNull(store);

            StringBuilder builder = new StringBuilder();
            builder.Append("\n\n## Memory\n\n");
            builder.Append("Facts the user asked you to keep across sessions. Use `recall` to read one in full or to search, ");
            builder.Append("`remember` to save a durable fact, preference, or decision the user wants kept (never secrets), ");
            builder.Append("and `forget` to remove one that is wrong or outdated. Memories may be stale; verify before relying on them.\n");

            List<MemoryEntry> project = store.List(MemoryScopeEnum.Project, workingDirectory);
            List<MemoryEntry> global = store.List(MemoryScopeEnum.Global, workingDirectory);
            if (project.Count + global.Count == 0)
            {
                builder.Append("\nNo memories are saved yet.\n");
                return builder.ToString();
            }

            IndexBudget budget = new IndexBudget { Limit = Math.Max(0, maxBytes) };
            AppendScope(builder, "Project memories", project, budget);
            AppendScope(builder, "Global memories", global, budget);
            if (budget.Omitted > 0)
            {
                builder.Append("\n(").Append(budget.Omitted).Append(budget.Omitted == 1 ? " older memory is" : " older memories are")
                    .Append(" not listed because of the memory index size limit; use recall to search them.)\n");
            }

            return builder.ToString();
        }

        /// <summary>
        /// Build the memory section from a memory root directory.
        /// </summary>
        /// <param name="memoryRoot">Memory root, for example <c>~/.xeno/memory</c>.</param>
        /// <param name="workingDirectory">Conversation's working directory, which selects the project.</param>
        /// <param name="maxBytes">Most UTF-8 bytes the index lines may use.</param>
        /// <returns>Section, starting with a blank line.</returns>
        /// <exception cref="ArgumentException">memoryRoot is blank.</exception>
        public static string Build(string memoryRoot, string? workingDirectory, int maxBytes)
        {
            return Build(new MemoryStore(memoryRoot), workingDirectory, maxBytes);
        }

        #endregion

        #region Private-Methods

        private static void AppendScope(StringBuilder builder, string heading, List<MemoryEntry> entries, IndexBudget budget)
        {
            if (entries.Count == 0) return;

            StringBuilder lines = new StringBuilder();
            foreach (MemoryEntry entry in entries)
            {
                string line = "- " + entry.Slug + ": " + entry.Description + "\n";
                int bytes = Encoding.UTF8.GetByteCount(line);

                // Entries are newest first; once one does not fit, it and everything after it are left out.
                if (budget.Full || budget.Used + bytes > budget.Limit)
                {
                    budget.Full = true;
                    budget.Omitted++;
                    continue;
                }

                budget.Used += bytes;
                lines.Append(line);
            }

            if (lines.Length > 0) builder.Append('\n').Append(heading).Append(":\n").Append(lines);
        }

        #endregion

        #region Private-Classes

        private sealed class IndexBudget
        {
            public int Limit { get; set; } = 0;

            public int Used { get; set; } = 0;

            public int Omitted { get; set; } = 0;

            public bool Full { get; set; } = false;
        }

        #endregion
    }
}

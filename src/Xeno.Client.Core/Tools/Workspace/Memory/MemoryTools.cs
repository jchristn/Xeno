namespace Xeno.Client.Tools.Workspace.Memory
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Creates the persistent memory tools, ported from Mux: remember and forget (mutating) and recall (read-only).
    /// </summary>
    public static class MemoryTools
    {
        #region Public-Methods

        /// <summary>
        /// Create the memory tools over one store.
        /// </summary>
        /// <param name="memoryRoot">Memory root, for example <c>Path.Combine(ClientSettings.SettingsDirectory, "memory")</c>.</param>
        /// <returns>remember, forget, and recall.</returns>
        /// <exception cref="ArgumentException">memoryRoot is blank.</exception>
        public static IReadOnlyList<IWorkspaceTool> Create(string memoryRoot)
        {
            MemoryStore store = new MemoryStore(memoryRoot);
            return new List<IWorkspaceTool> { new RememberTool(store), new ForgetTool(store), new RecallTool(store) };
        }

        #endregion
    }
}

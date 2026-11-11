namespace Xeno.Client.Tools.Workspace
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using Xeno.Client.Tools.Workspace.Files;
    using Xeno.Client.Tools.Workspace.Memory;
    using Xeno.Client.Tools.Workspace.Planning;
    using Xeno.Client.Tools.Workspace.Processes;
    using Xeno.Client.Tools.Workspace.Web;

    /// <summary>
    /// Every workspace tool ported from Mux, in offer order: files and search, processes, web, task planning, and memory.
    /// </summary>
    public static class WorkspaceTools
    {
        /// <summary>
        /// Create the tools.
        /// </summary>
        /// <param name="processes">Background processes, owned and disposed by the caller.</param>
        /// <param name="plans">Task plans per conversation.</param>
        /// <param name="memoryRoot">Folder memories are kept in.</param>
        /// <param name="webHandler">HTTP handler for web tools, for tests; null for the default.</param>
        /// <returns>Tools.</returns>
        /// <exception cref="ArgumentNullException">processes or plans is null.</exception>
        /// <exception cref="ArgumentException">memoryRoot is empty.</exception>
        public static IReadOnlyList<IWorkspaceTool> Create(BackgroundProcessRegistry processes, TaskPlanStore plans, string memoryRoot, HttpMessageHandler? webHandler = null)
        {
            ArgumentNullException.ThrowIfNull(processes);
            ArgumentNullException.ThrowIfNull(plans);
            if (String.IsNullOrWhiteSpace(memoryRoot)) throw new ArgumentException("A memory folder is required.", nameof(memoryRoot));
            List<IWorkspaceTool> tools = new List<IWorkspaceTool>();
            tools.AddRange(FileTools.Create());
            tools.AddRange(ProcessTools.Create(processes));
            tools.AddRange(WebTools.Create(webHandler));
            tools.AddRange(PlanningTools.Create(plans));
            tools.AddRange(MemoryTools.Create(memoryRoot));
            return tools;
        }
    }
}

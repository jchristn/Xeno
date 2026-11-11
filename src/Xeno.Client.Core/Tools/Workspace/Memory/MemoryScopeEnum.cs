namespace Xeno.Client.Tools.Workspace.Memory
{
    /// <summary>
    /// Where a memory applies: to the current project only, or to every project.
    /// </summary>
    public enum MemoryScopeEnum
    {
        /// <summary>
        /// The memory belongs to one project, keyed by the repository root of the conversation's working directory (or
        /// the working directory itself outside git).
        /// </summary>
        Project = 0,

        /// <summary>
        /// The memory applies everywhere (user-wide preferences and facts).
        /// </summary>
        Global = 1
    }
}

namespace Xeno.Client.Tools.Workspace.Memory
{
    using System;

    /// <summary>
    /// One remembered fact: a short name (its slug is the file name), a one-line description used in the prompt index,
    /// the full content, its scope, and timestamps.
    /// </summary>
    public sealed class MemoryEntry
    {
        #region Public-Members

        /// <summary>
        /// Name as given (also used, slugged, as the file name).
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Slug of the name: lowercase letters, digits, and hyphens.
        /// </summary>
        public string Slug { get; set; } = String.Empty;

        /// <summary>
        /// One-line summary shown in the prompt index.
        /// </summary>
        public string Description { get; set; } = String.Empty;

        /// <summary>
        /// Full text.
        /// </summary>
        public string Content { get; set; } = String.Empty;

        /// <summary>
        /// Where the memory applies.
        /// </summary>
        public MemoryScopeEnum Scope { get; set; } = MemoryScopeEnum.Project;

        /// <summary>
        /// When the memory was first saved (UTC).
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.MinValue;

        /// <summary>
        /// When the memory was last changed (UTC).
        /// </summary>
        public DateTime UpdatedUtc { get; set; } = DateTime.MinValue;

        /// <summary>
        /// File that holds the memory.
        /// </summary>
        public string FilePath { get; set; } = String.Empty;

        #endregion
    }
}

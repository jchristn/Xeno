namespace Xeno.Client.Tools.Workspace.Files
{
    using System.Collections.Generic;

    /// <summary>
    /// The file and search workspace tools ported from Mux.
    /// </summary>
    public static class FileTools
    {
        /// <summary>
        /// A new instance of every file tool: read_file, write_file, edit_file, multi_edit, delete_file, file_metadata,
        /// list_directory, manage_directory, glob, and grep.
        /// </summary>
        /// <returns>Tools.</returns>
        public static IReadOnlyList<IWorkspaceTool> Create()
        {
            return new List<IWorkspaceTool>
            {
                new ReadFileTool(),
                new WriteFileTool(),
                new EditFileTool(),
                new MultiEditTool(),
                new DeleteFileTool(),
                new FileMetadataTool(),
                new ListDirectoryTool(),
                new ManageDirectoryTool(),
                new GlobTool(),
                new GrepTool()
            };
        }
    }
}

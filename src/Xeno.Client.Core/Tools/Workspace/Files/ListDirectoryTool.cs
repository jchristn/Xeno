namespace Xeno.Client.Tools.Workspace.Files
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Chat.Wire;

    /// <summary>
    /// list_directory: the directories, then the files, in one directory. Symbolic links that lead outside the working
    /// directory are left out.
    /// </summary>
    public sealed class ListDirectoryTool : IWorkspaceTool
    {
        #region Public-Members

        /// <inheritdoc />
        public string Name => "list_directory";

        /// <inheritdoc />
        public string Description => "Lists files and directories at a given path. Directories are listed first (marked [DIR]), then files (marked [FILE]), sorted alphabetically within each group.";

        /// <inheritdoc />
        public ChatToolSchemaDto Schema => WorkspaceSchema.Object(
            WorkspaceSchema.Text("path", "The path to the directory to list, relative to the working directory (an absolute path must be inside it; \".\" is the working directory)."),
            "path");

        /// <inheritdoc />
        public bool Mutating => false;

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public Task<object> InvokeAsync(string arguments, WorkspaceToolContext context, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(context);
            ListDirectoryArguments args = WorkspaceJson.Read<ListDirectoryArguments>(arguments);
            string resolved = WorkspacePath.Resolve(args.Path, context.WorkingDirectory);
            if (!Directory.Exists(resolved)) throw new DirectoryNotFoundException("Directory not found: " + WorkspacePath.Relative(resolved, context.WorkingDirectory));

            List<string> directories = new List<string>();
            List<string> files = new List<string>();
            foreach (FileSystemInfo entry in new DirectoryInfo(resolved).EnumerateFileSystemInfos())
            {
                token.ThrowIfCancellationRequested();
                if (entry.LinkTarget != null && !FileWalker.IsInside(entry.FullName, context.WorkingDirectory)) continue;
                if (entry is DirectoryInfo) directories.Add(entry.Name);
                else files.Add(entry.Name);
            }

            directories.Sort(StringComparer.OrdinalIgnoreCase);
            files.Sort(StringComparer.OrdinalIgnoreCase);
            StringBuilder builder = new StringBuilder();
            foreach (string directory in directories) builder.Append("[DIR]  ").Append(directory).Append('\n');
            foreach (string file in files) builder.Append("[FILE] ").Append(file).Append('\n');
            return Task.FromResult<object>(builder.ToString());
        }

        #endregion
    }
}

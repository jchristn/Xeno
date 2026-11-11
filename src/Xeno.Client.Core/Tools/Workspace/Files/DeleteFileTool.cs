namespace Xeno.Client.Tools.Workspace.Files
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Chat.Wire;

    /// <summary>
    /// delete_file: deletes one file.
    /// </summary>
    public sealed class DeleteFileTool : IWorkspaceTool
    {
        #region Public-Members

        /// <inheritdoc />
        public string Name => "delete_file";

        /// <inheritdoc />
        public string Description => "Deletes a file from the filesystem. Returns an error if the file does not exist.";

        /// <inheritdoc />
        public ChatToolSchemaDto Schema => WorkspaceSchema.Object(
            WorkspaceSchema.Text("file_path", "The path to the file to delete, relative to the working directory (an absolute path must be inside it)."),
            "file_path");

        /// <inheritdoc />
        public bool Mutating => true;

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public Task<object> InvokeAsync(string arguments, WorkspaceToolContext context, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(context);
            token.ThrowIfCancellationRequested();
            DeleteFileArguments args = WorkspaceJson.Read<DeleteFileArguments>(arguments);
            string filePath = FileToolText.RequiredPath(args.FilePath, "file_path");
            string resolved = WorkspacePath.Resolve(filePath, context.WorkingDirectory);
            string relative = WorkspacePath.Relative(resolved, context.WorkingDirectory);
            if (Directory.Exists(resolved)) throw new IOException(relative + " is a directory. Use manage_directory to delete a directory.");
            if (!File.Exists(resolved)) throw new FileNotFoundException("File not found: " + relative);
            File.Delete(resolved);
            return Task.FromResult<object>(new DeleteFileResult { FilePath = relative });
        }

        #endregion
    }
}

namespace Xeno.Client.Tools.Workspace.Files
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Chat.Wire;

    /// <summary>
    /// file_metadata: size, timestamps, and attributes of a file or directory.
    /// </summary>
    public sealed class FileMetadataTool : IWorkspaceTool
    {
        #region Public-Members

        /// <inheritdoc />
        public string Name => "file_metadata";

        /// <inheritdoc />
        public string Description => "Returns metadata about a file or directory including size, creation time, last modified time, last access time, and attributes. Works for both files and directories.";

        /// <inheritdoc />
        public ChatToolSchemaDto Schema => WorkspaceSchema.Object(
            WorkspaceSchema.Text("path", "The path to the file or directory, relative to the working directory (an absolute path must be inside it)."),
            "path");

        /// <inheritdoc />
        public bool Mutating => false;

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public Task<object> InvokeAsync(string arguments, WorkspaceToolContext context, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(context);
            token.ThrowIfCancellationRequested();
            FileMetadataArguments args = WorkspaceJson.Read<FileMetadataArguments>(arguments);
            string path = FileToolText.RequiredPath(args.Path, "path");
            string resolved = WorkspacePath.Resolve(path, context.WorkingDirectory);
            string relative = WorkspacePath.Relative(resolved, context.WorkingDirectory);

            if (File.Exists(resolved))
            {
                FileInfo file = new FileInfo(resolved);
                return Task.FromResult<object>(new FileMetadataResult
                {
                    Path = relative,
                    Type = "file",
                    SizeBytes = file.Length,
                    CreatedUtc = file.CreationTimeUtc.ToString("o"),
                    ModifiedUtc = file.LastWriteTimeUtc.ToString("o"),
                    AccessedUtc = file.LastAccessTimeUtc.ToString("o"),
                    IsReadonly = file.IsReadOnly,
                    Extension = file.Extension
                });
            }

            if (Directory.Exists(resolved))
            {
                DirectoryInfo directory = new DirectoryInfo(resolved);
                int files = 0;
                int directories = 0;
                try
                {
                    files = directory.GetFiles().Length;
                    directories = directory.GetDirectories().Length;
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
                {
                    // The contents cannot be listed; report the rest.
                }

                return Task.FromResult<object>(new FileMetadataResult
                {
                    Path = relative,
                    Type = "directory",
                    CreatedUtc = directory.CreationTimeUtc.ToString("o"),
                    ModifiedUtc = directory.LastWriteTimeUtc.ToString("o"),
                    AccessedUtc = directory.LastAccessTimeUtc.ToString("o"),
                    FileCount = files,
                    DirectoryCount = directories
                });
            }

            throw new FileNotFoundException("Path not found: " + relative);
        }

        #endregion
    }
}

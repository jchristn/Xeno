namespace Xeno.Client.Tools.Workspace.Files
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Chat.Wire;

    /// <summary>
    /// write_file: writes content to a file, creating parent directories, and keeps an existing file's line endings
    /// and encoding.
    /// </summary>
    public sealed class WriteFileTool : IWorkspaceTool
    {
        #region Public-Members

        /// <inheritdoc />
        public string Name => "write_file";

        /// <inheritdoc />
        public string Description => "Writes content to a file. Creates parent directories if they do not exist. Preserves original line ending style for existing files; uses platform default for new files.";

        /// <inheritdoc />
        public ChatToolSchemaDto Schema => WorkspaceSchema.Object(
            WorkspaceSchema.Text("file_path", "The path to the file to write, relative to the working directory (an absolute path must be inside it)."),
            WorkspaceSchema.Text("content", "The content to write to the file."),
            "file_path",
            "content");

        /// <inheritdoc />
        public bool Mutating => true;

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<object> InvokeAsync(string arguments, WorkspaceToolContext context, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(context);
            WriteFileArguments args = WorkspaceJson.Read<WriteFileArguments>(arguments);
            string filePath = FileToolText.RequiredPath(args.FilePath, "file_path");
            string content = FileToolText.Required(args.Content, "content");
            string resolved = WorkspacePath.Resolve(filePath, context.WorkingDirectory);
            string relative = WorkspacePath.Relative(resolved, context.WorkingDirectory);
            if (Directory.Exists(resolved)) throw new IOException(relative + " is a directory.");

            string lineEnding = File.Exists(resolved) ? FileToolText.DetectFileLineEnding(resolved) : Environment.NewLine;
            string? parent = Path.GetDirectoryName(resolved);
            if (parent != null && !Directory.Exists(parent)) Directory.CreateDirectory(parent);

            string normalized = FileToolText.ToLf(content);
            string output = normalized.Replace("\n", lineEnding);
            await File.WriteAllTextAsync(resolved, output, FileTextEncoding.Detect(resolved), token).ConfigureAwait(false);
            return new WriteFileResult { FilePath = relative, LineCount = FileToolText.LineCount(normalized) };
        }

        #endregion
    }
}

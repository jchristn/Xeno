namespace Xeno.Client.Tools.Workspace.Files
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Chat.Wire;

    /// <summary>
    /// read_file: reads a file and returns its lines numbered like cat -n, optionally a range of lines.
    /// </summary>
    public sealed class ReadFileTool : IWorkspaceTool
    {
        #region Public-Members

        /// <summary>
        /// Largest file read whole; a larger file must be read in ranges with offset and limit.
        /// </summary>
        public const long MaxInlineBytes = 1_048_576;

        /// <inheritdoc />
        public string Name => "read_file";

        /// <inheritdoc />
        public string Description => "Reads a file from the filesystem and returns its contents with line numbers (like cat -n). Supports optional offset and limit parameters to read a specific range of lines. Files larger than " + MaxInlineBytes + " bytes must be read in ranges with offset and limit.";

        /// <inheritdoc />
        public ChatToolSchemaDto Schema => WorkspaceSchema.Object(
            WorkspaceSchema.Text("file_path", "The path to the file to read, relative to the working directory (an absolute path must be inside it)."),
            WorkspaceSchema.Integer("offset", "The line number to start reading from (1-based). Defaults to 1."),
            WorkspaceSchema.Integer("limit", "The maximum number of lines to read. Defaults to reading the entire file."),
            "file_path");

        /// <inheritdoc />
        public bool Mutating => false;

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<object> InvokeAsync(string arguments, WorkspaceToolContext context, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(context);
            ReadFileArguments args = WorkspaceJson.Read<ReadFileArguments>(arguments);
            string filePath = FileToolText.RequiredPath(args.FilePath, "file_path");
            string resolved = WorkspacePath.Resolve(filePath, context.WorkingDirectory);
            string relative = WorkspacePath.Relative(resolved, context.WorkingDirectory);

            if (Directory.Exists(resolved)) throw new IOException(relative + " is a directory. Use list_directory to see what it contains.");
            if (!File.Exists(resolved)) throw new FileNotFoundException("File not found: " + relative);

            int offset = args.Offset ?? 1;
            int limit = args.Limit ?? -1;
            bool hasRange = args.Offset != null || args.Limit != null;
            long length = new FileInfo(resolved).Length;
            if (length > MaxInlineBytes && !hasRange)
            {
                throw new IOException("File size (" + length + " bytes) exceeds the inline limit (" + MaxInlineBytes + " bytes): " + relative + ". Use read_file with offset and limit to read a specific range of lines, or grep to search it.");
            }

            int start = Math.Max(1, offset);
            StringBuilder builder = new StringBuilder();
            using (StreamReader reader = new StreamReader(resolved, FileTextEncoding.Detect(resolved), detectEncodingFromByteOrderMarks: true))
            {
                int lineNumber = 0;
                int written = 0;
                while (limit <= 0 || written < limit)
                {
                    string? line = await reader.ReadLineAsync(token).ConfigureAwait(false);
                    if (line == null) break;
                    lineNumber++;
                    if (lineNumber < start) continue;
                    builder.Append(lineNumber.ToString().PadLeft(6)).Append('\t').Append(line.Replace("\r", String.Empty)).Append('\n');
                    written++;
                }
            }

            return builder.ToString();
        }

        #endregion
    }
}

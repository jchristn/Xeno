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
    /// edit_file: replaces an exact string in a file. Matching ignores line-ending differences and the file keeps its
    /// line endings and encoding.
    /// </summary>
    public sealed class EditFileTool : IWorkspaceTool
    {
        #region Public-Members

        /// <inheritdoc />
        public string Name => "edit_file";

        /// <inheritdoc />
        public string Description => "Performs an exact string replacement in a file. Finds old_string and replaces it with new_string. Returns an error if the old_string is not found or matches multiple locations (ambiguous); set replace_all to replace every occurrence instead.";

        /// <inheritdoc />
        public ChatToolSchemaDto Schema => WorkspaceSchema.Object(
            WorkspaceSchema.Text("file_path", "The path to the file to edit, relative to the working directory (an absolute path must be inside it)."),
            WorkspaceSchema.Text("old_string", "The exact string to find and replace."),
            WorkspaceSchema.Text("new_string", "The replacement string."),
            WorkspaceSchema.Bool("replace_all", "Replace every occurrence of old_string instead of requiring exactly one. Defaults to false."),
            "file_path",
            "old_string",
            "new_string");

        /// <inheritdoc />
        public bool Mutating => true;

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<object> InvokeAsync(string arguments, WorkspaceToolContext context, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(context);
            EditFileArguments args = WorkspaceJson.Read<EditFileArguments>(arguments);
            string filePath = FileToolText.RequiredPath(args.FilePath, "file_path");
            string oldString = FileToolText.Required(args.OldString, "old_string");
            string newString = FileToolText.Required(args.NewString, "new_string");
            if (oldString.Length == 0) throw new ArgumentException("old_string must not be empty. Use write_file to create or overwrite a file.");
            string resolved = WorkspacePath.Resolve(filePath, context.WorkingDirectory);
            string relative = WorkspacePath.Relative(resolved, context.WorkingDirectory);
            if (!File.Exists(resolved)) throw new FileNotFoundException("File not found: " + relative);

            Encoding encoding = FileTextEncoding.Detect(resolved);
            string original = await File.ReadAllTextAsync(resolved, encoding, token).ConfigureAwait(false);
            string lineEnding = FileToolText.DetectLineEnding(original);
            string lfContent = FileToolText.ToLf(original);
            string lfOld = FileToolText.ToLf(oldString);
            string lfNew = FileToolText.ToLf(newString);

            List<int> matches = FileToolText.FindAll(lfContent, lfOld);
            if (matches.Count == 0) throw new InvalidOperationException("The specified old_string was not found in " + relative + ".");

            string lfResult;
            int applied;
            if (args.ReplaceAll == true)
            {
                applied = CountNonOverlapping(lfContent, lfOld);
                lfResult = lfContent.Replace(lfOld, lfNew, StringComparison.Ordinal);
            }
            else
            {
                if (matches.Count > 1)
                {
                    List<string> lines = new List<string>();
                    foreach (int position in matches) lines.Add(FileToolText.LineOf(lfContent, position).ToString());
                    throw new InvalidOperationException("old_string matches " + matches.Count + " locations in " + relative + " (lines " + String.Join(", ", lines) + "). Provide more surrounding context in old_string to uniquely identify the target location, or set replace_all.");
                }

                applied = 1;
                lfResult = lfContent.Substring(0, matches[0]) + lfNew + lfContent.Substring(matches[0] + lfOld.Length);
            }

            await File.WriteAllTextAsync(resolved, lfResult.Replace("\n", lineEnding), encoding, token).ConfigureAwait(false);
            return new EditFileResult { FilePath = relative, EditsApplied = applied, NewLineCount = FileToolText.LineCount(lfResult) };
        }

        #endregion

        #region Private-Methods

        private static int CountNonOverlapping(string content, string search)
        {
            int count = 0;
            int index = 0;
            while (true)
            {
                int found = content.IndexOf(search, index, StringComparison.Ordinal);
                if (found < 0) return count;
                count++;
                index = found + search.Length;
            }
        }

        #endregion
    }
}

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
    /// multi_edit: several exact replacements in one file, all or none. Every edit must match exactly once in the
    /// original content before anything is applied; the edits then apply in order and the file is written once.
    /// </summary>
    public sealed class MultiEditTool : IWorkspaceTool
    {
        #region Public-Members

        /// <inheritdoc />
        public string Name => "multi_edit";

        /// <inheritdoc />
        public string Description => "Performs multiple sequential string replacements in a single file. All edits are validated before any are applied. Each edit modifies the working content for subsequent edits.";

        /// <inheritdoc />
        public ChatToolSchemaDto Schema => WorkspaceSchema.Object(
            WorkspaceSchema.Text("file_path", "The path to the file to edit, relative to the working directory (an absolute path must be inside it)."),
            WorkspaceSchema.Array("edits", "An array of edit operations to apply sequentially.", WorkspaceSchema.Nested(
                "One replacement.",
                WorkspaceSchema.Text("old_string", "The exact string to find and replace."),
                WorkspaceSchema.Text("new_string", "The replacement string."),
                "old_string",
                "new_string")),
            "file_path",
            "edits");

        /// <inheritdoc />
        public bool Mutating => true;

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<object> InvokeAsync(string arguments, WorkspaceToolContext context, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(context);
            MultiEditArguments args = WorkspaceJson.Read<MultiEditArguments>(arguments);
            string filePath = FileToolText.RequiredPath(args.FilePath, "file_path");
            if (args.Edits == null || args.Edits.Count == 0) throw new ArgumentException("Parameter 'edits' is required and must be a non-empty array.");
            List<string> olds = new List<string>();
            List<string> news = new List<string>();
            for (int i = 0; i < args.Edits.Count; i++)
            {
                MultiEditOperation? edit = args.Edits[i];
                if (edit == null || edit.OldString == null) throw new ArgumentException("Edit at index " + i + ": required parameter 'old_string' is missing or not a string.");
                if (edit.NewString == null) throw new ArgumentException("Edit at index " + i + ": required parameter 'new_string' is missing or not a string.");
                if (edit.OldString.Length == 0) throw new ArgumentException("Edit at index " + i + ": old_string must not be empty.");
                olds.Add(FileToolText.ToLf(edit.OldString));
                news.Add(FileToolText.ToLf(edit.NewString));
            }

            string resolved = WorkspacePath.Resolve(filePath, context.WorkingDirectory);
            string relative = WorkspacePath.Relative(resolved, context.WorkingDirectory);
            if (!File.Exists(resolved)) throw new FileNotFoundException("File not found: " + relative);

            Encoding encoding = FileTextEncoding.Detect(resolved);
            string original = await File.ReadAllTextAsync(resolved, encoding, token).ConfigureAwait(false);
            string lineEnding = FileToolText.DetectLineEnding(original);
            string lfContent = FileToolText.ToLf(original);

            for (int i = 0; i < olds.Count; i++)
            {
                int count = FileToolText.FindAll(lfContent, olds[i]).Count;
                if (count == 0) throw new InvalidOperationException("Edit at index " + i + ": old_string was not found in the original file content. No edits were applied.");
                if (count > 1) throw new InvalidOperationException("Edit at index " + i + ": old_string matches " + count + " locations. Provide more context to uniquely identify the target. No edits were applied.");
            }

            string working = lfContent;
            for (int i = 0; i < olds.Count; i++)
            {
                int position = working.IndexOf(olds[i], StringComparison.Ordinal);
                if (position < 0) throw new InvalidOperationException("Edit at index " + i + ": old_string no longer found after applying previous edits. No edits were applied.");
                working = working.Substring(0, position) + news[i] + working.Substring(position + olds[i].Length);
            }

            await File.WriteAllTextAsync(resolved, working.Replace("\n", lineEnding), encoding, token).ConfigureAwait(false);
            return new EditFileResult { FilePath = relative, EditsApplied = olds.Count, NewLineCount = FileToolText.LineCount(working) };
        }

        #endregion
    }
}

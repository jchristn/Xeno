namespace Xeno.Client.Tools.Workspace.Files
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Chat.Wire;

    /// <summary>
    /// glob: files whose path, relative to the search directory, matches a glob pattern. Symbolic links to directories
    /// are not followed and links that lead outside the working directory are left out.
    /// </summary>
    public sealed class GlobTool : IWorkspaceTool
    {
        #region Public-Members

        /// <inheritdoc />
        public string Name => "glob";

        /// <inheritdoc />
        public string Description => "Searches for files matching a glob pattern. Supports * (any characters in filename), ** (any path segments), and ? (single character). Returned paths are relative to the working directory.";

        /// <inheritdoc />
        public ChatToolSchemaDto Schema => WorkspaceSchema.Object(
            WorkspaceSchema.Text("pattern", "The glob pattern to match files against (e.g., '**/*.cs', 'src/**/*.json')."),
            WorkspaceSchema.Text("path", "The directory to search in. Defaults to the working directory."),
            "pattern");

        /// <inheritdoc />
        public bool Mutating => false;

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public Task<object> InvokeAsync(string arguments, WorkspaceToolContext context, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(context);
            GlobArguments args = WorkspaceJson.Read<GlobArguments>(arguments);
            string pattern = FileToolText.RequiredPath(args.Pattern, "pattern");
            string resolved = WorkspacePath.Resolve(args.Path, context.WorkingDirectory);
            if (!Directory.Exists(resolved)) throw new DirectoryNotFoundException("Directory not found: " + WorkspacePath.Relative(resolved, context.WorkingDirectory));

            Regex regex = FileWalker.GlobToRegex(pattern);
            List<string> matches = new List<string>();
            foreach (string file in FileWalker.EnumerateFiles(resolved, context.WorkingDirectory, token))
            {
                string searchRelative = Path.GetRelativePath(resolved, file).Replace('\\', '/');
                if (regex.IsMatch(searchRelative)) matches.Add(WorkspacePath.Relative(file, context.WorkingDirectory));
            }

            matches.Sort(StringComparer.OrdinalIgnoreCase);
            StringBuilder builder = new StringBuilder();
            builder.Append("Found ").Append(matches.Count).Append(" matching file(s):\n");
            foreach (string match in matches) builder.Append(match).Append('\n');
            return Task.FromResult<object>(builder.ToString());
        }

        #endregion
    }
}

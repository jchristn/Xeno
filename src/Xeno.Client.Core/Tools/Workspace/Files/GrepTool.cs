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
    /// grep: lines matching a regular expression in files under a directory, at most <see cref="MaxMatches"/>. Binary
    /// files are skipped, symbolic links to directories are not followed, and links that lead outside the working
    /// directory are left out.
    /// </summary>
    public sealed class GrepTool : IWorkspaceTool
    {
        #region Public-Members

        /// <summary>
        /// Most matching lines returned.
        /// </summary>
        public const int MaxMatches = 100;

        /// <inheritdoc />
        public string Name => "grep";

        /// <inheritdoc />
        public string Description => "Searches files recursively for lines matching a regular expression. Returns matching lines with file path and line number. Limited to the first " + MaxMatches + " matches. Paths are relative to the working directory.";

        /// <inheritdoc />
        public ChatToolSchemaDto Schema => WorkspaceSchema.Object(
            WorkspaceSchema.Text("pattern", "The regular expression pattern to search for."),
            WorkspaceSchema.Text("path", "The directory to search in. Defaults to the working directory."),
            WorkspaceSchema.Text("include", "A glob pattern to filter which files to search (e.g., '*.cs', '*.json')."),
            "pattern");

        /// <inheritdoc />
        public bool Mutating => false;

        #endregion

        #region Private-Members

        private const int _BinaryProbeBytes = 8000;
        private const long _MaxFileBytes = 16_777_216;

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<object> InvokeAsync(string arguments, WorkspaceToolContext context, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(context);
            GrepArguments args = WorkspaceJson.Read<GrepArguments>(arguments);
            string pattern = FileToolText.Required(args.Pattern, "pattern");
            string resolved = WorkspacePath.Resolve(args.Path, context.WorkingDirectory);
            if (!Directory.Exists(resolved)) throw new DirectoryNotFoundException("Directory not found: " + WorkspacePath.Relative(resolved, context.WorkingDirectory));

            Regex regex;
            try
            {
                regex = new Regex(pattern, RegexOptions.None, TimeSpan.FromSeconds(5));
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException("Invalid regular expression: " + ex.Message);
            }

            string? include = String.IsNullOrWhiteSpace(args.Include) ? null : args.Include.Trim();
            Regex? includeRegex = include == null ? null : FileWalker.GlobToRegex(include);
            bool includeByPath = include != null && (include.Contains('/') || include.Contains('\\'));

            List<string> files = new List<string>(FileWalker.EnumerateFiles(resolved, context.WorkingDirectory, token));
            files.Sort(StringComparer.OrdinalIgnoreCase);

            StringBuilder builder = new StringBuilder();
            int matchCount = 0;
            foreach (string file in files)
            {
                token.ThrowIfCancellationRequested();
                if (matchCount >= MaxMatches) break;
                if (includeRegex != null)
                {
                    string subject = includeByPath ? Path.GetRelativePath(resolved, file).Replace('\\', '/') : Path.GetFileName(file);
                    if (!includeRegex.IsMatch(subject)) continue;
                }

                List<string> lines;
                try
                {
                    if (new FileInfo(file).Length > _MaxFileBytes || IsBinary(file)) continue;
                    lines = await ReadLinesAsync(file, token).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    continue;
                }

                string relative = WorkspacePath.Relative(file, context.WorkingDirectory);
                for (int i = 0; i < lines.Count && matchCount < MaxMatches; i++)
                {
                    bool matched;
                    try
                    {
                        matched = regex.IsMatch(lines[i]);
                    }
                    catch (RegexMatchTimeoutException)
                    {
                        break;
                    }

                    if (!matched) continue;
                    builder.Append(relative).Append(':').Append(i + 1).Append(": ").Append(lines[i]).Append('\n');
                    matchCount++;
                }
            }

            if (matchCount == 0) builder.Append("No matches found.\n");
            else if (matchCount >= MaxMatches) builder.Append("(output truncated at ").Append(MaxMatches).Append(" matches)\n");
            return builder.ToString();
        }

        #endregion

        #region Private-Methods

        private static bool IsBinary(string file)
        {
            using (FileStream stream = File.OpenRead(file))
            {
                byte[] buffer = new byte[_BinaryProbeBytes];
                int read = stream.Read(buffer, 0, buffer.Length);
                if (read >= 2 && ((buffer[0] == 0xff && buffer[1] == 0xfe) || (buffer[0] == 0xfe && buffer[1] == 0xff))) return false;
                for (int i = 0; i < read; i++)
                {
                    if (buffer[i] == 0) return true;
                }

                return false;
            }
        }

        private static async Task<List<string>> ReadLinesAsync(string file, CancellationToken token)
        {
            List<string> lines = new List<string>();
            using (StreamReader reader = new StreamReader(file, FileTextEncoding.Detect(file), detectEncodingFromByteOrderMarks: true))
            {
                while (true)
                {
                    string? line = await reader.ReadLineAsync(token).ConfigureAwait(false);
                    if (line == null) return lines;
                    lines.Add(line);
                }
            }
        }

        #endregion
    }
}

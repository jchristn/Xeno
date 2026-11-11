namespace Xeno.Client.Tools.Workspace.Files
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;

    /// <summary>
    /// Walks directories for glob and grep without leaving the working directory: symbolic links to directories are
    /// never descended into, and symbolic links to files are returned only when their target is inside.
    /// </summary>
    internal static class FileWalker
    {
        /// <summary>
        /// Every file under a directory, recursively.
        /// </summary>
        /// <param name="root">Full path of a directory inside the working directory.</param>
        /// <param name="workingDirectory">Working directory.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Full file paths.</returns>
        internal static IEnumerable<string> EnumerateFiles(string root, string workingDirectory, CancellationToken token)
        {
            Stack<string> pending = new Stack<string>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                token.ThrowIfCancellationRequested();
                string directory = pending.Pop();
                List<FileSystemInfo> entries = new List<FileSystemInfo>();
                try
                {
                    entries.AddRange(new DirectoryInfo(directory).EnumerateFileSystemInfos());
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
                {
                    continue;
                }

                foreach (FileSystemInfo entry in entries)
                {
                    if (entry.LinkTarget != null)
                    {
                        if (entry is FileInfo && IsInside(entry.FullName, workingDirectory)) yield return entry.FullName;
                        continue;
                    }

                    if (entry is DirectoryInfo) pending.Push(entry.FullName);
                    else yield return entry.FullName;
                }
            }
        }

        /// <summary>
        /// True when a path, with its symbolic links followed, is inside the working directory.
        /// </summary>
        /// <param name="fullPath">Full path.</param>
        /// <param name="workingDirectory">Working directory.</param>
        /// <returns>True when inside.</returns>
        internal static bool IsInside(string fullPath, string workingDirectory)
        {
            try
            {
                WorkspacePath.Resolve(fullPath, workingDirectory);
                return true;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is ArgumentException || ex is IOException)
            {
                return false;
            }
        }

        /// <summary>
        /// A case-insensitive regular expression for a glob: * matches within a path segment, ** matches any segments,
        /// ? matches one character, and {a,b} matches either alternative.
        /// </summary>
        /// <param name="pattern">Glob.</param>
        /// <returns>Regular expression matched against forward-slash relative paths.</returns>
        internal static Regex GlobToRegex(string pattern)
        {
            string normalized = pattern.Replace('\\', '/');
            StringBuilder regex = new StringBuilder("^");
            int i = 0;
            int braces = 0;
            while (i < normalized.Length)
            {
                char c = normalized[i];
                if (c == '*' && i + 1 < normalized.Length && normalized[i + 1] == '*')
                {
                    if (i + 2 < normalized.Length && normalized[i + 2] == '/')
                    {
                        regex.Append("(.+/)?");
                        i += 3;
                    }
                    else
                    {
                        regex.Append(".*");
                        i += 2;
                    }
                }
                else if (c == '*')
                {
                    regex.Append("[^/]*");
                    i++;
                }
                else if (c == '?')
                {
                    regex.Append("[^/]");
                    i++;
                }
                else if (c == '{')
                {
                    regex.Append('(');
                    braces++;
                    i++;
                }
                else if (c == '}' && braces > 0)
                {
                    regex.Append(')');
                    braces--;
                    i++;
                }
                else if (c == ',' && braces > 0)
                {
                    regex.Append('|');
                    i++;
                }
                else
                {
                    regex.Append(Regex.Escape(c.ToString()));
                    i++;
                }
            }

            while (braces-- > 0) regex.Append(')');
            regex.Append('$');
            return new Regex(regex.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5));
        }
    }
}

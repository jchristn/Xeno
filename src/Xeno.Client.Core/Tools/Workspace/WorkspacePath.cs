namespace Xeno.Client.Tools.Workspace
{
    using System;
    using System.IO;

    /// <summary>
    /// Resolves paths for workspace tools and keeps them inside the conversation's working directory, following
    /// symbolic links in the part of the path that exists so a link cannot lead outside.
    /// </summary>
    public static class WorkspacePath
    {
        private static readonly StringComparison _Comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        /// <summary>
        /// Resolve a path the model gave (relative to the working directory, or absolute) to a full path inside the
        /// working directory.
        /// </summary>
        /// <param name="path">Path from the model; null or empty means the working directory itself.</param>
        /// <param name="workingDirectory">Absolute working directory.</param>
        /// <returns>Full path.</returns>
        /// <exception cref="ArgumentException">workingDirectory is empty, or the path is malformed.</exception>
        /// <exception cref="UnauthorizedAccessException">The path is outside the working directory.</exception>
        public static string Resolve(string? path, string workingDirectory)
        {
            if (String.IsNullOrWhiteSpace(workingDirectory)) throw new ArgumentException("There is no working directory for this conversation.");
            string root = Real(Path.GetFullPath(workingDirectory));
            string candidate;
            try
            {
                string expanded = String.IsNullOrWhiteSpace(path) ? "." : ExpandHome(path.Trim());
                candidate = Path.GetFullPath(Path.IsPathRooted(expanded) ? expanded : Path.Combine(workingDirectory, expanded));
            }
            catch (Exception ex) when (ex is NotSupportedException || ex is PathTooLongException || ex is ArgumentException)
            {
                throw new ArgumentException("'" + path + "' is not a valid path: " + ex.Message);
            }

            string real = Real(candidate);
            if (!IsWithin(real, root)) throw new UnauthorizedAccessException("'" + path + "' is outside this conversation's working directory (" + workingDirectory + ").");
            return candidate;
        }

        /// <summary>
        /// The path relative to the working directory, with forward slashes, for results shown to the model.
        /// </summary>
        /// <param name="fullPath">Full path.</param>
        /// <param name="workingDirectory">Working directory.</param>
        /// <returns>Relative path, or "." for the directory itself.</returns>
        public static string Relative(string fullPath, string workingDirectory)
        {
            string relative = Path.GetRelativePath(Path.GetFullPath(workingDirectory), fullPath);
            return relative.Replace('\\', '/');
        }

        /// <summary>
        /// True when a full path is the root or inside it.
        /// </summary>
        /// <param name="fullPath">Full path.</param>
        /// <param name="root">Full root path.</param>
        /// <returns>True when inside.</returns>
        public static bool IsWithin(string fullPath, string root)
        {
            string trimmedRoot = Path.TrimEndingDirectorySeparator(root);
            string trimmed = Path.TrimEndingDirectorySeparator(fullPath);
            if (String.Equals(trimmed, trimmedRoot, _Comparison)) return true;
            return trimmed.StartsWith(trimmedRoot + Path.DirectorySeparatorChar, _Comparison);
        }

        private static string ExpandHome(string path)
        {
            if (path == "~") return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (path.StartsWith("~/", StringComparison.Ordinal) || path.StartsWith("~\\", StringComparison.Ordinal)) return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path.Substring(2));
            return path;
        }

        // The path with symbolic links resolved in its longest existing prefix; the missing rest is appended unchanged.
        private static string Real(string fullPath)
        {
            string existing = fullPath;
            string rest = String.Empty;
            while (!File.Exists(existing) && !Directory.Exists(existing))
            {
                string? parent = Path.GetDirectoryName(existing);
                if (parent == null) return fullPath;
                rest = rest.Length == 0 ? Path.GetFileName(existing) : Path.Combine(Path.GetFileName(existing), rest);
                existing = parent;
            }

            string resolved = ResolveLinks(existing);
            return rest.Length == 0 ? resolved : Path.Combine(resolved, rest);
        }

        private static string ResolveLinks(string existing)
        {
            string? parent = Path.GetDirectoryName(existing);
            string resolvedParent = parent == null ? existing : ResolveLinks(parent);
            string current = parent == null ? existing : Path.Combine(resolvedParent, Path.GetFileName(existing));
            FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            if (info.LinkTarget == null) return current;
            FileSystemInfo? target;
            try
            {
                target = info.ResolveLinkTarget(true);
            }
            catch (IOException)
            {
                throw new UnauthorizedAccessException("'" + current + "' is a symbolic link that cannot be followed.");
            }

            // The final target is not a link, but folders on its path may be (on macOS /var is a link to /private/var).
            return target == null ? current : ResolveLinks(Path.GetFullPath(target.FullName));
        }
    }
}

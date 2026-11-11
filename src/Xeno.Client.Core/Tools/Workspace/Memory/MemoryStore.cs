namespace Xeno.Client.Tools.Workspace.Memory
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;

    /// <summary>
    /// Persistent memory: facts the user or the model asked to keep across conversations, in the same format as Mux.
    /// Each memory is one Markdown file with a small frontmatter (name, description, scope, created and updated times)
    /// under <c>&lt;root&gt;/&lt;project-key&gt;/</c> for project memories or <c>&lt;root&gt;/global/</c> for user-wide
    /// ones, and each folder has a <c>MEMORY.md</c> index rewritten on every change. Saving a memory whose name slugs to
    /// an existing one updates it. Files without a valid frontmatter are ignored. Writes are atomic (temporary file,
    /// then move). Thread safe within a process.
    /// </summary>
    public sealed class MemoryStore
    {
        #region Public-Members

        /// <summary>
        /// Name of the global scope's folder.
        /// </summary>
        public const string GlobalFolderName = "global";

        /// <summary>
        /// Name of the index file in each folder.
        /// </summary>
        public const string IndexFileName = "MEMORY.md";

        /// <summary>
        /// Memory root directory.
        /// </summary>
        public string RootDirectory
        {
            get { return _Root; }
        }

        /// <summary>
        /// Raised after a memory is saved or deleted.
        /// </summary>
        public event EventHandler? Changed;

        #endregion

        #region Private-Members

        private const int MaxSlugLength = 60;
        private const int MaxDescriptionLength = 200;
        private const int MaxContentLength = 20000;

        private readonly object _Sync = new object();
        private readonly string _Root;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the store.
        /// </summary>
        /// <param name="rootDirectory">Memory root, for example <c>~/.xeno/memory</c>. Created on first write.</param>
        /// <exception cref="ArgumentException">The root is blank.</exception>
        public MemoryStore(string rootDirectory)
        {
            if (String.IsNullOrWhiteSpace(rootDirectory)) throw new ArgumentException("A memory directory is required.", nameof(rootDirectory));
            _Root = Path.GetFullPath(rootDirectory);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Convert a name to its file slug: lowercase letters and digits joined by single hyphens, at most 60 characters.
        /// </summary>
        /// <param name="name">Name.</param>
        /// <returns>Slug, or empty when the name has no letters or digits.</returns>
        public static string Slugify(string? name)
        {
            StringBuilder builder = new StringBuilder();
            bool hyphen = false;
            foreach (char c in (name ?? String.Empty).Trim().ToLowerInvariant())
            {
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                {
                    if (hyphen && builder.Length > 0) builder.Append('-');
                    builder.Append(c);
                    hyphen = false;
                }
                else
                {
                    hyphen = true;
                }

                if (builder.Length >= MaxSlugLength) break;
            }

            return builder.ToString().Trim('-');
        }

        /// <summary>
        /// Folder key for a working directory's project: the repository root's folder name (the nearest ancestor with a
        /// <c>.git</c> entry, or the directory itself outside git) plus a short hash of its full path, so two checkouts
        /// with the same name do not share memories.
        /// </summary>
        /// <param name="workingDirectory">Working directory; blank means the process's current directory.</param>
        /// <returns>Project key.</returns>
        public static string ProjectKey(string? workingDirectory)
        {
            string directory = String.IsNullOrWhiteSpace(workingDirectory) ? Directory.GetCurrentDirectory() : workingDirectory;
            string root = FindProjectRoot(directory) ?? directory;
            string full = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string normalized = OperatingSystem.IsWindows() ? full.ToLowerInvariant() : full;
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
            string leaf = Slugify(Path.GetFileName(full));
            if (leaf.Length == 0) leaf = "project";
            if (leaf.Length > 40) leaf = leaf.Substring(0, 40).Trim('-');
            return leaf + "-" + Convert.ToHexString(hash, 0, 6).ToLowerInvariant();
        }

        /// <summary>
        /// Parse a scope name: project (or repo, local) and global (or user), case-insensitive.
        /// </summary>
        /// <param name="value">Text.</param>
        /// <param name="scope">Parsed scope.</param>
        /// <returns>True when recognized.</returns>
        public static bool TryParseScope(string? value, out MemoryScopeEnum scope)
        {
            scope = MemoryScopeEnum.Project;
            switch ((value ?? String.Empty).Trim().ToLowerInvariant())
            {
                case "project":
                case "repo":
                case "local":
                    scope = MemoryScopeEnum.Project;
                    return true;
                case "global":
                case "user":
                    scope = MemoryScopeEnum.Global;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Lowercase name of a scope as the tools and files spell it.
        /// </summary>
        /// <param name="scope">Scope.</param>
        /// <returns>project or global.</returns>
        public static string ScopeName(MemoryScopeEnum scope)
        {
            return scope == MemoryScopeEnum.Global ? "global" : "project";
        }

        /// <summary>
        /// Folder holding a scope's memories for a working directory.
        /// </summary>
        /// <param name="scope">Scope.</param>
        /// <param name="workingDirectory">Working directory (ignored for the global scope).</param>
        /// <returns>Folder path; it may not exist yet.</returns>
        public string DirectoryFor(MemoryScopeEnum scope, string? workingDirectory)
        {
            return Path.Combine(_Root, scope == MemoryScopeEnum.Global ? GlobalFolderName : ProjectKey(workingDirectory));
        }

        /// <summary>
        /// A scope's memories, newest first.
        /// </summary>
        /// <param name="scope">Scope.</param>
        /// <param name="workingDirectory">Working directory.</param>
        /// <returns>Memories.</returns>
        public List<MemoryEntry> List(MemoryScopeEnum scope, string? workingDirectory)
        {
            string directory = DirectoryFor(scope, workingDirectory);
            List<MemoryEntry> entries;
            lock (_Sync)
            {
                entries = ReadFolderNoLock(directory, scope);
            }

            entries.Sort(delegate (MemoryEntry a, MemoryEntry b)
            {
                int byTime = b.UpdatedUtc.CompareTo(a.UpdatedUtc);
                return byTime != 0 ? byTime : String.CompareOrdinal(a.Slug, b.Slug);
            });
            return entries;
        }

        /// <summary>
        /// The project's memories followed by the global ones, each newest first.
        /// </summary>
        /// <param name="workingDirectory">Working directory.</param>
        /// <returns>Memories.</returns>
        public List<MemoryEntry> ListAll(string? workingDirectory)
        {
            List<MemoryEntry> all = List(MemoryScopeEnum.Project, workingDirectory);
            all.AddRange(List(MemoryScopeEnum.Global, workingDirectory));
            return all;
        }

        /// <summary>
        /// Find a memory by name or slug. Without a scope, the project is searched before the global scope.
        /// </summary>
        /// <param name="name">Name or slug.</param>
        /// <param name="scope">Scope, or null for project then global.</param>
        /// <param name="workingDirectory">Working directory.</param>
        /// <returns>Memory, or null.</returns>
        public MemoryEntry? Get(string? name, MemoryScopeEnum? scope, string? workingDirectory)
        {
            string slug = Slugify(name);
            if (slug.Length == 0) return null;

            foreach (MemoryScopeEnum candidate in Scopes(scope))
            {
                string file = Path.Combine(DirectoryFor(candidate, workingDirectory), slug + ".md");
                lock (_Sync)
                {
                    if (File.Exists(file))
                    {
                        MemoryEntry? entry = TryRead(file, candidate);
                        if (entry != null) return entry;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Save a memory, creating it or updating the one whose name has the same slug.
        /// </summary>
        /// <param name="name">Name; must contain a letter or digit.</param>
        /// <param name="description">One-line summary; the first line of the content is used when blank.</param>
        /// <param name="content">Full text; the description is used when blank.</param>
        /// <param name="scope">Scope.</param>
        /// <param name="workingDirectory">Working directory.</param>
        /// <param name="created">True when a new memory was created, false when an existing one was updated.</param>
        /// <returns>Saved memory.</returns>
        /// <exception cref="ArgumentException">The name has no letters or digits, or both description and content are blank.</exception>
        public MemoryEntry Save(string name, string? description, string? content, MemoryScopeEnum scope, string? workingDirectory, out bool created)
        {
            string slug = Slugify(name);
            if (slug.Length == 0) throw new ArgumentException("A memory name needs at least one letter or digit.", nameof(name));
            string body = (content ?? String.Empty).Trim();
            string summary = OneLine(description);
            if (summary.Length == 0) summary = OneLine(FirstLine(body));
            if (summary.Length == 0) throw new ArgumentException("A memory needs a description or content.", nameof(description));
            if (body.Length == 0) body = summary;
            if (body.Length > MaxContentLength) body = body.Substring(0, MaxContentLength);
            if (summary.Length > MaxDescriptionLength) summary = summary.Substring(0, MaxDescriptionLength - 3).TrimEnd() + "...";

            string directory = DirectoryFor(scope, workingDirectory);
            string file = Path.Combine(directory, slug + ".md");
            MemoryEntry entry;
            lock (_Sync)
            {
                Directory.CreateDirectory(directory);
                MemoryEntry? existing = File.Exists(file) ? TryRead(file, scope) : null;
                DateTime now = DateTime.UtcNow;
                entry = new MemoryEntry
                {
                    Name = OneLine(name),
                    Slug = slug,
                    Description = summary,
                    Content = body,
                    Scope = scope,
                    CreatedUtc = existing?.CreatedUtc ?? now,
                    UpdatedUtc = now,
                    FilePath = file
                };
                created = existing == null;
                WriteAtomic(file, Serialize(entry));
                WriteIndexNoLock(directory, scope);
            }

            OnChanged();
            return entry;
        }

        /// <summary>
        /// Delete a memory by name. Without a scope, the project is tried before the global scope.
        /// </summary>
        /// <param name="name">Name or slug.</param>
        /// <param name="scope">Scope, or null.</param>
        /// <param name="workingDirectory">Working directory.</param>
        /// <returns>Deleted memory, or null when none matched.</returns>
        public MemoryEntry? Delete(string? name, MemoryScopeEnum? scope, string? workingDirectory)
        {
            MemoryEntry? entry = Get(name, scope, workingDirectory);
            if (entry == null) return null;

            lock (_Sync)
            {
                File.Delete(entry.FilePath);
                WriteIndexNoLock(Path.GetDirectoryName(entry.FilePath)!, entry.Scope);
            }

            OnChanged();
            return entry;
        }

        /// <summary>
        /// Delete every memory in a scope.
        /// </summary>
        /// <param name="scope">Scope.</param>
        /// <param name="workingDirectory">Working directory.</param>
        /// <returns>Number deleted.</returns>
        public int Clear(MemoryScopeEnum scope, string? workingDirectory)
        {
            List<MemoryEntry> entries = List(scope, workingDirectory);
            if (entries.Count == 0) return 0;

            string directory = DirectoryFor(scope, workingDirectory);
            lock (_Sync)
            {
                foreach (MemoryEntry entry in entries) File.Delete(entry.FilePath);
                WriteIndexNoLock(directory, scope);
            }

            OnChanged();
            return entries.Count;
        }

        /// <summary>
        /// Search the project and global memories. Every whitespace-separated term must appear (case-insensitive) in the
        /// name, description, or content. A blank query returns everything.
        /// </summary>
        /// <param name="query">Search text.</param>
        /// <param name="workingDirectory">Working directory.</param>
        /// <returns>Matches, project first, newest first within each scope.</returns>
        public List<MemoryEntry> Search(string? query, string? workingDirectory)
        {
            string[] terms = (query ?? String.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            List<MemoryEntry> matches = new List<MemoryEntry>();
            foreach (MemoryEntry entry in ListAll(workingDirectory))
            {
                string haystack = entry.Name + "\n" + entry.Description + "\n" + entry.Content;
                bool all = true;
                foreach (string term in terms)
                {
                    if (haystack.IndexOf(term, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        all = false;
                        break;
                    }
                }

                if (all) matches.Add(entry);
            }

            return matches;
        }

        #endregion

        #region Private-Methods

        private static string? FindProjectRoot(string workingDirectory)
        {
            string? full;
            try
            {
                full = Path.GetFullPath(workingDirectory);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
            {
                return null;
            }

            string? current = full;
            while (!String.IsNullOrEmpty(current))
            {
                string marker = Path.Combine(current, ".git");
                if (Directory.Exists(marker) || File.Exists(marker)) return Path.TrimEndingDirectorySeparator(current);
                current = Directory.GetParent(current)?.FullName;
            }

            return Path.TrimEndingDirectorySeparator(full);
        }

        private static IEnumerable<MemoryScopeEnum> Scopes(MemoryScopeEnum? scope)
        {
            if (scope.HasValue)
            {
                yield return scope.Value;
                yield break;
            }

            yield return MemoryScopeEnum.Project;
            yield return MemoryScopeEnum.Global;
        }

        private static List<MemoryEntry> ReadFolderNoLock(string directory, MemoryScopeEnum scope)
        {
            List<MemoryEntry> entries = new List<MemoryEntry>();
            if (!Directory.Exists(directory)) return entries;

            foreach (string file in Directory.EnumerateFiles(directory, "*.md"))
            {
                if (String.Equals(Path.GetFileName(file), IndexFileName, StringComparison.OrdinalIgnoreCase)) continue;
                MemoryEntry? entry = TryRead(file, scope);
                if (entry != null) entries.Add(entry);
            }

            return entries;
        }

        private static MemoryEntry? TryRead(string file, MemoryScopeEnum scope)
        {
            string text;
            try
            {
                text = File.ReadAllText(file);
            }
            catch (Exception)
            {
                return null;
            }

            string normalized = text.Replace("\r\n", "\n");
            if (!normalized.StartsWith("---\n", StringComparison.Ordinal)) return null;

            int end = normalized.IndexOf("\n---", 4, StringComparison.Ordinal);
            if (end < 0) return null;

            Dictionary<string, string> fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string line in normalized.Substring(4, end - 4).Split('\n'))
            {
                int colon = line.IndexOf(':');
                if (colon <= 0) continue;
                fields[line.Substring(0, colon).Trim()] = line.Substring(colon + 1).Trim();
            }

            if (!fields.TryGetValue("name", out string? name) || String.IsNullOrWhiteSpace(name)) return null;

            int bodyStart = normalized.IndexOf('\n', end + 4);
            string body = bodyStart < 0 ? String.Empty : normalized.Substring(bodyStart + 1).Trim();
            fields.TryGetValue("description", out string? description);
            return new MemoryEntry
            {
                Name = name,
                Slug = Path.GetFileNameWithoutExtension(file),
                Description = description ?? String.Empty,
                Content = body,
                Scope = scope,
                CreatedUtc = ParseTime(fields, "created", file),
                UpdatedUtc = ParseTime(fields, "updated", file),
                FilePath = file
            };
        }

        private static DateTime ParseTime(Dictionary<string, string> fields, string key, string file)
        {
            if (fields.TryGetValue(key, out string? value)
                && DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime parsed))
            {
                return parsed;
            }

            try
            {
                return File.GetLastWriteTimeUtc(file);
            }
            catch (Exception)
            {
                return DateTime.MinValue;
            }
        }

        private static string Serialize(MemoryEntry entry)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("---\n");
            builder.Append("name: ").Append(entry.Name).Append('\n');
            builder.Append("description: ").Append(entry.Description).Append('\n');
            builder.Append("scope: ").Append(ScopeName(entry.Scope)).Append('\n');
            builder.Append("created: ").Append(entry.CreatedUtc.ToString("o", CultureInfo.InvariantCulture)).Append('\n');
            builder.Append("updated: ").Append(entry.UpdatedUtc.ToString("o", CultureInfo.InvariantCulture)).Append('\n');
            builder.Append("---\n\n");
            builder.Append(entry.Content).Append('\n');
            return builder.ToString();
        }

        private static void WriteIndexNoLock(string directory, MemoryScopeEnum scope)
        {
            if (!Directory.Exists(directory)) return;

            List<MemoryEntry> entries = ReadFolderNoLock(directory, scope);
            entries.Sort(delegate (MemoryEntry a, MemoryEntry b) { return b.UpdatedUtc.CompareTo(a.UpdatedUtc); });
            StringBuilder builder = new StringBuilder();
            builder.Append("# Memory index (").Append(ScopeName(scope)).Append(")\n\n");
            foreach (MemoryEntry entry in entries)
            {
                builder.Append("- [").Append(entry.Name).Append("](").Append(entry.Slug).Append(".md): ").Append(entry.Description).Append('\n');
            }

            WriteAtomic(Path.Combine(directory, IndexFileName), builder.ToString());
        }

        private static void WriteAtomic(string path, string content)
        {
            string temp = path + "." + Guid.NewGuid().ToString("N").Substring(0, 8) + ".tmp";
            File.WriteAllText(temp, content, new UTF8Encoding(false));
            File.Move(temp, path, true);
        }

        private static string OneLine(string? text)
        {
            return (text ?? String.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
        }

        private static string FirstLine(string text)
        {
            int newline = text.IndexOf('\n');
            return newline < 0 ? text : text.Substring(0, newline);
        }

        private void OnChanged()
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }

        #endregion
    }
}

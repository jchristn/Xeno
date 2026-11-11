namespace Xeno.Client.Tools.Workspace.Files
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Chat.Wire;

    /// <summary>
    /// manage_directory: creates, deletes, or renames a directory inside the working directory. The working directory
    /// itself cannot be deleted or renamed.
    /// </summary>
    public sealed class ManageDirectoryTool : IWorkspaceTool
    {
        #region Public-Members

        /// <inheritdoc />
        public string Name => "manage_directory";

        /// <inheritdoc />
        public string Description => "Creates, deletes, or renames a directory. Use action 'create' to create a directory (including parents), 'delete' to remove an empty or non-empty directory, or 'rename' to move/rename a directory.";

        /// <inheritdoc />
        public ChatToolSchemaDto Schema => WorkspaceSchema.Object(
            WorkspaceSchema.Choice("action", "The action to perform: 'create', 'delete', or 'rename'.", "create", "delete", "rename"),
            WorkspaceSchema.Text("path", "The path to the directory to create, delete, or rename from, relative to the working directory (an absolute path must be inside it)."),
            WorkspaceSchema.Text("new_path", "The new path for the directory (required for 'rename' action)."),
            "action",
            "path");

        /// <inheritdoc />
        public bool Mutating => true;

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public Task<object> InvokeAsync(string arguments, WorkspaceToolContext context, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(context);
            token.ThrowIfCancellationRequested();
            ManageDirectoryArguments args = WorkspaceJson.Read<ManageDirectoryArguments>(arguments);
            string action = FileToolText.Required(args.Action, "action").Trim().ToLowerInvariant();
            string path = FileToolText.RequiredPath(args.Path, "path");
            string resolved = WorkspacePath.Resolve(path, context.WorkingDirectory);
            string relative = WorkspacePath.Relative(resolved, context.WorkingDirectory);

            switch (action)
            {
                case "create":
                    if (Directory.Exists(resolved)) return Task.FromResult<object>(new ManageDirectoryResult { Path = relative, Message = "Directory already exists." });
                    if (File.Exists(resolved)) throw new IOException("A file already exists at " + relative + ".");
                    Directory.CreateDirectory(resolved);
                    return Task.FromResult<object>(new ManageDirectoryResult { Path = relative });

                case "delete":
                    RejectRoot(resolved, context.WorkingDirectory, "delete");
                    if (!Directory.Exists(resolved)) throw new DirectoryNotFoundException("Directory not found: " + relative);
                    if (new DirectoryInfo(resolved).LinkTarget != null) Directory.Delete(resolved);
                    else Directory.Delete(resolved, true);
                    return Task.FromResult<object>(new ManageDirectoryResult { Path = relative });

                case "rename":
                    if (String.IsNullOrWhiteSpace(args.NewPath)) throw new ArgumentException("The 'new_path' parameter is required for the 'rename' action.");
                    RejectRoot(resolved, context.WorkingDirectory, "rename");
                    string target = WorkspacePath.Resolve(args.NewPath, context.WorkingDirectory);
                    string targetRelative = WorkspacePath.Relative(target, context.WorkingDirectory);
                    if (!Directory.Exists(resolved)) throw new DirectoryNotFoundException("Source directory not found: " + relative);
                    if (Directory.Exists(target) || File.Exists(target)) throw new IOException("Target already exists: " + targetRelative);
                    string? parent = Path.GetDirectoryName(target);
                    if (parent != null && !Directory.Exists(parent)) Directory.CreateDirectory(parent);
                    Directory.Move(resolved, target);
                    return Task.FromResult<object>(new ManageDirectoryResult { FromPath = relative, ToPath = targetRelative });

                default:
                    throw new ArgumentException("Unknown action '" + action + "'. Use 'create', 'delete', or 'rename'.");
            }
        }

        #endregion

        #region Private-Methods

        // The working directory, under any spelling, is the one directory inside it whose parent is outside it.
        private static void RejectRoot(string resolved, string workingDirectory, string action)
        {
            string parent = Path.GetFullPath(Path.Combine(resolved, ".."));
            if (FileWalker.IsInside(parent, workingDirectory) && !String.Equals(WorkspacePath.Relative(resolved, workingDirectory), ".", StringComparison.Ordinal)) return;
            throw new UnauthorizedAccessException("The working directory itself cannot be " + (action == "delete" ? "deleted" : "renamed") + ".");
        }

        #endregion
    }
}

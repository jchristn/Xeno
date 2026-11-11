namespace Xeno.Client.Tools.Workspace
{
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Chat.Wire;

    /// <summary>
    /// A chat tool that works on this machine (files, processes, the web, a task plan, memory), ported from Mux. Every
    /// path a tool touches is confined to the conversation's working directory through <see cref="WorkspacePath"/>.
    /// </summary>
    public interface IWorkspaceTool
    {
        /// <summary>
        /// Name offered to the model, for example read_file.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// What the tool does, written for the model.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// JSON schema of the arguments.
        /// </summary>
        ChatToolSchemaDto Schema { get; }

        /// <summary>
        /// True when the tool changes something (writes or deletes files, runs a process), so chat asks for Allow or
        /// Deny before it runs.
        /// </summary>
        bool Mutating { get; }

        /// <summary>
        /// Run the tool.
        /// </summary>
        /// <param name="arguments">Arguments as JSON text.</param>
        /// <param name="context">Conversation, working directory, and settings.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A result object; the host serializes it to JSON and truncates it to the configured size.</returns>
        /// <exception cref="System.ArgumentException">The arguments are invalid.</exception>
        /// <exception cref="System.UnauthorizedAccessException">A path is outside the working directory.</exception>
        Task<object> InvokeAsync(string arguments, WorkspaceToolContext context, CancellationToken token);
    }
}

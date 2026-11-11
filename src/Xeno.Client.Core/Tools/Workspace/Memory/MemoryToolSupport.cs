namespace Xeno.Client.Tools.Workspace.Memory
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Xeno.Core.Chat.Wire;

    /// <summary>
    /// Shared pieces of the memory tools: the scope argument, argument cleanup, and error mapping (as Mux does).
    /// </summary>
    internal static class MemoryToolSupport
    {
        #region Internal-Methods

        /// <summary>
        /// The scope argument property.
        /// </summary>
        /// <returns>Property.</returns>
        internal static KeyValuePair<string, ChatToolPropertyDto> ScopeProperty()
        {
            return WorkspaceSchema.Choice("scope", "project (default): this repository only. global: every project.", "project", "global");
        }

        /// <summary>
        /// Blank text becomes null.
        /// </summary>
        /// <param name="value">Text.</param>
        /// <returns>Text or null.</returns>
        internal static string? Text(string? value)
        {
            return String.IsNullOrWhiteSpace(value) ? null : value;
        }

        /// <summary>
        /// Parse an optional scope argument.
        /// </summary>
        /// <param name="value">Text.</param>
        /// <returns>Scope, or null when omitted.</returns>
        /// <exception cref="ArgumentException">The scope is not recognized.</exception>
        internal static MemoryScopeEnum? Scope(string? value)
        {
            string? text = Text(value);
            if (text == null) return null;
            if (!MemoryStore.TryParseScope(text, out MemoryScopeEnum scope)) throw new ArgumentException("scope must be 'project' or 'global'.");
            return scope;
        }

        /// <summary>
        /// Run a tool body, turning argument and file errors into error results the model can read.
        /// </summary>
        /// <param name="body">Body.</param>
        /// <returns>Result.</returns>
        internal static object Run(Func<object> body)
        {
            try
            {
                return body();
            }
            catch (ArgumentException ex)
            {
                return new MemoryErrorResult { Error = "invalid_arguments", Message = ex.Message };
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return new MemoryErrorResult { Error = "memory_io_error", Message = ex.Message };
            }
        }

        #endregion
    }
}

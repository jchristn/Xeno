namespace Xeno.Client.Tools.Workspace
{
    using System;
    using System.IO;
    using Xeno.Core.Serialization;

    /// <summary>
    /// Reads workspace tool arguments into named argument classes.
    /// </summary>
    public static class WorkspaceJson
    {
        /// <summary>
        /// Read arguments.
        /// </summary>
        /// <typeparam name="T">Argument class.</typeparam>
        /// <param name="arguments">JSON text; empty means no arguments.</param>
        /// <returns>Arguments.</returns>
        /// <exception cref="ArgumentException">The text is not a JSON object of this shape.</exception>
        public static T Read<T>(string? arguments) where T : class, new()
        {
            if (String.IsNullOrWhiteSpace(arguments)) return new T();
            try
            {
                return JsonUtil.Deserialize<T>(arguments) ?? new T();
            }
            catch (InvalidDataException ex)
            {
                throw new ArgumentException("The tool arguments are not valid: " + (ex.InnerException?.Message ?? ex.Message));
            }
        }
    }
}

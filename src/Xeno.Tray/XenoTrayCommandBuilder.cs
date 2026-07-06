namespace Xeno.Tray
{
    using System.Collections.Generic;

    /// <summary>
    /// Builds xeno CLI command arguments used by the tray companion.
    /// </summary>
    public static class XenoTrayCommandBuilder
    {
        /// <summary>
        /// Build arguments for starting the client.
        /// </summary>
        /// <returns>Arguments.</returns>
        public static IReadOnlyList<string> BuildStartArguments()
        {
            return new List<string> { "--json", "start" };
        }

        /// <summary>
        /// Build arguments for stopping the client.
        /// </summary>
        /// <returns>Arguments.</returns>
        public static IReadOnlyList<string> BuildStopArguments()
        {
            return new List<string> { "--json", "stop" };
        }
    }
}
